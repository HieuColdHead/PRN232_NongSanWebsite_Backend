using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BLL.Services
{
    public class ProductService : IProductService
    {
        private readonly IGenericRepository<Product> _repository;
        private readonly IGenericRepository<Category> _categoryRepository;
        private readonly IGenericRepository<Provider> _providerRepository;
        private readonly IGenericRepository<ProductImage> _productImageRepository;
        private readonly IGenericRepository<ProductVariant> _productVariantRepository;

        public ProductService(
            IGenericRepository<Product> repository,
            IGenericRepository<Category> categoryRepository,
            IGenericRepository<Provider> providerRepository,
            IGenericRepository<ProductImage> productImageRepository,
            IGenericRepository<ProductVariant> productVariantRepository)
        {
            _repository = repository;
            _categoryRepository = categoryRepository;
            _providerRepository = providerRepository;
            _productImageRepository = productImageRepository;
            _productVariantRepository = productVariantRepository;
        }

        public async Task<IEnumerable<ProductDto>> GetAllAsync()
        {
            var products = await _repository.GetAllAsync();
            var dtos = new List<ProductDto>();
            foreach (var product in products)
            {
                dtos.Add(await MapToDto(product));
            }
            return dtos;
        }

        public async Task<PagedResult<ProductDto>> GetPagedAsync(int pageNumber, int pageSize)
        {
            var (items, totalCount) = await _repository.GetPagedAsync(pageNumber, pageSize);
            var dtos = new List<ProductDto>();
            foreach (var item in items)
            {
                dtos.Add(await MapToDto(item));
            }
            
            return new PagedResult<ProductDto>
            {
                Items = dtos,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };
        }

        public async Task<ProductDto?> GetByIdAsync(Guid id)
        {
            var product = await _repository.GetByIdAsync(id);
            if (product == null) return null;
            return await MapToDto(product);
        }

        public async Task<ProductDto> CreateAsync(CreateProductRequest request)
        {
            var product = new Product
            {
                ProductId = Guid.NewGuid(),
                ProductName = request.Name,
                Description = request.Description,
                Origin = request.Origin,
                Unit = request.Unit,
                BasePrice = request.BasePrice,
                DiscountPrice = request.DiscountPrice,
                IsOrganic = request.IsOrganic,
                Status = request.Status,
                CategoryId = request.CategoryId,
                ProviderId = request.ProviderId,
                CreatedAt = DateTime.UtcNow
            };

            if (request.Images != null)
            {
                foreach (var image in request.Images)
                {
                    product.ProductImages.Add(new ProductImage
                    {
                        ImageId = Guid.NewGuid(),
                        ImageUrl = image.ImageUrl,
                        IsPrimary = image.IsPrimary,
                        ProductId = product.ProductId
                    });
                }
            }


            await _repository.AddAsync(product);
            await _repository.SaveChangesAsync();
            return (await MapToDto(product));
        }

        public async Task UpdateAsync(Guid id, UpdateProductRequest request)
        {
            var product = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Product {id} not found");

            // Update parent product properties
            if (request.Name != null) product.ProductName = request.Name;
            if (request.Description != null) product.Description = request.Description;
            if (request.Origin != null) product.Origin = request.Origin;
            if (request.Unit != null) product.Unit = request.Unit;
            if (request.BasePrice.HasValue) product.BasePrice = request.BasePrice.Value;
            if (request.DiscountPrice.HasValue) product.DiscountPrice = request.DiscountPrice.Value;
            if (request.IsOrganic.HasValue) product.IsOrganic = request.IsOrganic.Value;
            if (request.Status != null) product.Status = request.Status;
            if (request.CategoryId.HasValue) product.CategoryId = request.CategoryId.Value;
            if (request.ProviderId.HasValue) product.ProviderId = request.ProviderId.Value;
            product.UpdatedAt = DateTime.UtcNow;
            
            await _repository.UpdateAsync(product);

            // Update child collections
            if (request.Images != null)
            {
                await _productImageRepository.DeleteRangeAsync(product.ProductImages.ToList());
                var newImages = request.Images.Select(img => new ProductImage
                {
                    ImageId = Guid.NewGuid(),
                    ImageUrl = img.ImageUrl,
                    IsPrimary = img.IsPrimary,
                    ProductId = product.ProductId
                }).ToList();
                await _productImageRepository.AddRangeAsync(newImages);
            }


            // Only call SaveChanges once after all operations
            await _repository.SaveChangesAsync();
        }

        public async Task DeleteAsync(Guid id)
        {
            await _repository.DeleteAsync(id);
            await _repository.SaveChangesAsync();
        }

        private async Task<ProductDto> MapToDto(Product product)
        {
            var category = product.CategoryId.HasValue ? await _categoryRepository.GetByIdAsync(product.CategoryId.Value) : null;
            var provider = product.ProviderId.HasValue ? await _providerRepository.GetByIdAsync(product.ProviderId.Value) : null;

            return new ProductDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Description = product.Description,
                Origin = product.Origin,
                Unit = product.Unit,
                BasePrice = product.BasePrice,
                DiscountPrice = product.DiscountPrice,
                Quantity = product.ProductVariants.Sum(v => v.StockQuantity),
                IsOrganic = product.IsOrganic,
                Status = product.Status,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt,
                CategoryId = product.CategoryId,
                CategoryName = category?.CategoryName,
                ProviderId = product.ProviderId,
                ProviderName = provider?.ProviderName,
                IsDeleted = product.IsDeleted,
                ProductImages = product.ProductImages.Select(pi => new ProductImageDto
                {
                    ImageId = pi.ImageId,
                    ImageUrl = pi.ImageUrl,
                    IsPrimary = pi.IsPrimary,
                    ProductId = product.ProductId
                }).ToList(),
                ProductVariants = product.ProductVariants.Select(pv => new ProductVariantDto
                {
                    VariantId = pv.VariantId,
                    VariantName = pv.VariantName,
                    Price = pv.Price,
                    DiscountPrice = pv.DiscountPrice,
                    StockQuantity = pv.StockQuantity,
                    Sku = pv.Sku,
                    Status = pv.Status,
                    ProductId = product.ProductId
                }).ToList()
            };
        }
    }
}
