using AngleSharp;
using BLL.Services.Interfaces;
using CodeHollow.FeedReader;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Html.Parser;

namespace BLL.Services
{
    public class ArticleRssService : IArticleRssService
    {
        private readonly IGenericRepository<Blog> _blogRepository;
        private readonly IGenericRepository<User> _userRepository;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly ILogger<ArticleRssService> _logger;
        private const string RssUserName = "Automated RSS Fetcher";

        public ArticleRssService(
            IGenericRepository<Blog> blogRepository,
            IGenericRepository<User> userRepository,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<ArticleRssService> logger)
        {
            _blogRepository = blogRepository;
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<int> FetchAndSaveArticlesAsync()
        {
            _logger.LogInformation("Starting RSS fetch process.");

            var feedUrls = _configuration.GetSection("RssSettings:FeedUrls").Get<string[]>() ?? Array.Empty<string>();
            var keywords = _configuration.GetSection("RssSettings:Keywords").Get<string[]>() ?? Array.Empty<string>();

            if (!feedUrls.Any() || !keywords.Any())
            {
                _logger.LogWarning("RSS Feed URLs or Keywords are not configured in appsettings.json.");
                return 0;
            }
            
            var author = await GetOrCreateRssUserAsync();
            var newArticlesFound = new List<Blog>();

            foreach (var url in feedUrls)
            {
                try
                {
                    var reader = await FeedReader.ReadAsync(url);
                    _logger.LogInformation("Successfully read RSS feed from {Url}", url);

                    var filteredItems = reader.Items
                        .Where(item => keywords.Any(k => 
                            (item.Title != null && item.Title.ToLower().Contains(k)) || 
                            (item.Description != null && item.Description.ToLower().Contains(k))
                         ))
                        .ToList();

                    foreach (var item in filteredItems)
                    {
                        var alreadyExists = await _blogRepository.FirstOrDefaultAsync(b => b.Content == item.Link);
                        if (alreadyExists != null)
                        {
                            continue;
                        }

                        var thumbnailUrl = await ExtractImageUrlAsync(item);
                        var description = SanitizeHtml(item.Description);

                        var newArticle = new Blog
                        {
                            Title = item.Title,
                            Content = item.Link,
                            Description = description,
                            ThumbnailUrl = thumbnailUrl,
                            Source = reader.Title, // Get source from the feed's title
                            AuthorId = author.Id,
                            Status = "Published",
                            CreatedAt = item.PublishingDate?.ToUniversalTime() ?? DateTime.UtcNow
                        };
                        newArticlesFound.Add(newArticle);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read or process RSS feed from {Url}", url);
                }
            }

            if (newArticlesFound.Any())
            {
                await _blogRepository.AddRangeAsync(newArticlesFound);
                await _blogRepository.SaveChangesAsync();
                _logger.LogInformation("Successfully saved {Count} new articles from RSS feeds.", newArticlesFound.Count);
            }
            else
            {
                _logger.LogInformation("No new articles matching the keywords were found in the RSS feeds.");
            }

            return newArticlesFound.Count;
        }
        
        private string? SanitizeHtml(string? html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return null;
            }
            var parser = new HtmlParser();
            var document = parser.ParseDocument(html);
            return document.Body?.TextContent;
        }

        private async Task<string?> ExtractImageUrlAsync(FeedItem item)
        {
            // Priority 1: Check for Media-RSS namespace
            try
            {
                var mediaNamespace = "http://search.yahoo.com/mrss/";
                var mediaContent = item.SpecificItem.Element.Descendants(XName.Get("content", mediaNamespace)).FirstOrDefault();
                if (mediaContent?.Attribute("url") != null)
                {
                    return mediaContent.Attribute("url")!.Value;
                }
                var mediaThumbnail = item.SpecificItem.Element.Descendants(XName.Get("thumbnail", mediaNamespace)).FirstOrDefault();
                if (mediaThumbnail?.Attribute("url") != null)
                {
                    return mediaThumbnail.Attribute("url")!.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse media:content tag for item: {Title}", item.Title);
            }

            // Priority 2: Check for enclosure tag
            var enclosure = (item.SpecificItem as CodeHollow.FeedReader.Feeds.Rss20FeedItem)?.Enclosure;
            if (enclosure != null && enclosure.MediaType != null && enclosure.MediaType.StartsWith("image/"))
            {
                return enclosure.Url;
            }

            // Priority 3: Parse the description/content for an <img> tag
            string? contentToParse = item.Content ?? item.Description;
            if (!string.IsNullOrEmpty(contentToParse))
            {
                var context = BrowsingContext.New(AngleSharp.Configuration.Default);
                var document = await context.OpenAsync(req => req.Content(contentToParse));
                var firstImage = document.QuerySelector("img");
                if (firstImage != null && firstImage.HasAttribute("src"))
                {
                    return firstImage.GetAttribute("src");
                }
            }

            return null;
        }

        private async Task<User> GetOrCreateRssUserAsync()
        {
            var existingUser = await _userRepository.FirstOrDefaultAsync(u => u.DisplayName == RssUserName);
            if (existingUser != null)
            {
                return existingUser;
            }

            _logger.LogInformation("Creating a new user for the RSS service: {UserName}", RssUserName);
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                DisplayName = RssUserName,
                Email = "rss-fetcher@system.local",
                Provider = "System",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepository.AddAsync(newUser);
            await _userRepository.SaveChangesAsync();
            return newUser;
        }
    }
}
