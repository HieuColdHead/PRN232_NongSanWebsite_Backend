using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System;

namespace BLL.Services
{
    public class BlogService : IBlogService
    {
        private readonly IGenericRepository<Blog> _repository;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public BlogService(IGenericRepository<Blog> repository, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _repository = repository;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<PagedResult<BlogDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);

            return new PagedResult<BlogDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<BlogDto?> GetByIdAsync(Guid id)
        {
            var blog = await _repository.GetByIdAsync(id);
            if (blog == null) return null;
            return MapToDto(blog);
        }

        public async Task<PagedResult<BlogDto>> GetByAuthorIdAsync(Guid authorId, int pageNumber, int pageSize)
        {
            var all = await _repository.FindAsync(b => b.AuthorId == authorId);
            var totalCount = all.Count();
            var items = all
                .OrderByDescending(b => b.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize);

            return new PagedResult<BlogDto>
            {
                Items = items.Select(MapToDto),
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<BlogDto> CreateAsync(CreateBlogRequest request)
        {
            var blog = new Blog
            {
                Title = request.Title,
                Content = request.Content,
                ThumbnailUrl = request.ThumbnailUrl,
                AuthorId = request.AuthorId,
                CreatedAt = DateTime.UtcNow,
                Status = "Published"
            };

            await _repository.AddAsync(blog);
            await _repository.SaveChangesAsync();

            return MapToDto(blog);
        }

        public async Task UpdateAsync(Guid id, UpdateBlogRequest request)
        {
            var blog = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Blog {id} not found");

            if (request.Title != null) blog.Title = request.Title;
            if (request.Content != null) blog.Content = request.Content;
            if (request.ThumbnailUrl != null) blog.ThumbnailUrl = request.ThumbnailUrl;
            if (request.Status != null) blog.Status = request.Status;
            blog.UpdatedAt = DateTime.UtcNow;

            await _repository.UpdateAsync(blog);
            await _repository.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
        }

        public async Task<IEnumerable<ExternalArticleDto>> GetVietnameseAgricultureArticlesAsync()
        {
            var apiKey = _configuration["NewsApi:ApiKey"];
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"https://newsapi.org/v2/everything?q=nông%20sản%20việt%20nam&apiKey={apiKey}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var newsApiResponse = JsonSerializer.Deserialize<NewsApiResponse>(json);

                if (newsApiResponse?.Articles != null)
                {
                    return newsApiResponse.Articles.Select(a => new ExternalArticleDto
                    {
                        Title = a.Title,
                        Description = a.Description,
                        Url = a.Url,
                        ImageUrl = a.UrlToImage,
                        PublishedAt = a.PublishedAt,
                        SourceName = a.Source?.Name
                    });
                }
            }

            return Enumerable.Empty<ExternalArticleDto>();
        }

        private static BlogDto MapToDto(Blog blog)
        {
            return new BlogDto
            {
                BlogId = blog.BlogId,
                Title = blog.Title,
                Content = blog.Content,
                Description = blog.Description,
                ThumbnailUrl = blog.ThumbnailUrl,
                Source = blog.Source,
                Status = blog.Status,
                CreatedAt = blog.CreatedAt,
                UpdatedAt = blog.UpdatedAt,
                AuthorId = blog.AuthorId,
                AuthorName = blog.Author?.DisplayName
            };
        }

        private class NewsApiResponse
        {
            public string? Status { get; set; }
            public int TotalResults { get; set; }
            public List<Article>? Articles { get; set; }
        }

        private class Article
        {
            public Source? Source { get; set; }
            public string? Author { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? Url { get; set; }
            public string? UrlToImage { get; set; }
            public DateTime? PublishedAt { get; set; }
            public string? Content { get; set; }
        }

        private class Source
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
        }
    }
}
