using System.Threading.Tasks;

namespace BLL.Services.Interfaces
{
    public interface IArticleRssService
    {
        Task<int> FetchAndSaveArticlesAsync();
    }
}
