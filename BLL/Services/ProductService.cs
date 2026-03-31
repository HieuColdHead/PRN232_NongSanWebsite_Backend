using BLL.DTOs;
using BLL.Services.Interfaces;
using DAL.Data;
using DAL.Entity;
using DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
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
        private readonly ApplicationDbContext _context;

        public ProductService(
            IGenericRepository<Product> repository,
            IGenericRepository<Category> categoryRepository,
            IGenericRepository<Provider> providerRepository,
            IGenericRepository<ProductImage> productImageRepository,
            IGenericRepository<ProductVariant> productVariantRepository,
            ApplicationDbContext context)
        {
            _repository = repository;
            _categoryRepository = categoryRepository;
            _providerRepository = providerRepository;
            _productImageRepository = productImageRepository;
            _productVariantRepository = productVariantRepository;
            _context = context;
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

        public async Task<IEnumerable<ProductBestSellerDto>> GetBestSellersAsync(int top = 10, int? lastDays = null)
        {
            top = Math.Clamp(top, 1, 200);
            var since = lastDays.HasValue && lastDays.Value > 0
                ? DateTime.UtcNow.AddDays(-lastDays.Value)
                : (DateTime?)null;

            var deliveredOrders = _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.Status != null && o.Status.ToLower() == "delivered");

            if (since.HasValue)
            {
                deliveredOrders = deliveredOrders.Where(o => o.OrderDate >= since.Value);
            }

            var deliveredOrderIds = deliveredOrders.Select(o => o.OrderId);

            // 1) Variant lines -> ProductId, SoldQuantity (int -> decimal)
            var variantLines = await _context.Set<OrderDetail>()
                .AsNoTracking()
                .Where(d => deliveredOrderIds.Contains(d.OrderId) && d.VariantId.HasValue)
                .Join(
                    _context.Set<ProductVariant>().AsNoTracking(),
                    d => d.VariantId!.Value,
                    v => v.VariantId,
                    (d, v) => new { v.ProductId, Qty = (decimal)d.Quantity })
                .ToListAsync();

            // 2) Combo lines -> expand MealComboItems
            var comboLines = await _context.Set<OrderDetail>()
                .AsNoTracking()
                .Where(d => deliveredOrderIds.Contains(d.OrderId) && d.MealComboId.HasValue)
                .Select(d => new { MealComboId = d.MealComboId!.Value, ComboQty = d.Quantity })
                .ToListAsync();

            var soldByProductId = new Dictionary<Guid, decimal>();
            foreach (var line in variantLines)
            {
                soldByProductId[line.ProductId] = soldByProductId.TryGetValue(line.ProductId, out var current)
                    ? current + line.Qty
                    : line.Qty;
            }

            if (comboLines.Count > 0)
            {
                var comboIds = comboLines.Select(x => x.MealComboId).Distinct().ToList();
                var items = await _context.Set<MealComboItem>()
                    .AsNoTracking()
                    .Where(i => comboIds.Contains(i.MealComboId))
                    .Select(i => new { i.MealComboId, i.ProductId, i.Quantity })
                    .ToListAsync();

                var itemsByComboId = items
                    .GroupBy(i => i.MealComboId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var comboLine in comboLines)
                {
                    if (!itemsByComboId.TryGetValue(comboLine.MealComboId, out var comboItems))
                        continue;

                    var comboQty = Math.Max(1, comboLine.ComboQty);
                    foreach (var item in comboItems)
                    {
                        var qty = comboQty * item.Quantity;
                        if (qty <= 0) continue;
                        soldByProductId[item.ProductId] = soldByProductId.TryGetValue(item.ProductId, out var current)
                            ? current + qty
                            : qty;
                    }
                }
            }

            if (soldByProductId.Count == 0)
            {
                return Array.Empty<ProductBestSellerDto>();
            }

            var topIds = soldByProductId
                .OrderByDescending(kv => kv.Value)
                .Take(top)
                .Select(kv => kv.Key)
                .ToList();

            var products = await _context.Set<Product>()
                .AsNoTracking()
                .Where(p => !p.IsDeleted && topIds.Contains(p.ProductId))
                .Include(p => p.ProductImages)
                .ToListAsync();

            var productById = products.ToDictionary(p => p.ProductId, p => p);

            return topIds
                .Where(productById.ContainsKey)
                .Select(id =>
                {
                    var p = productById[id];
                    var imageUrl = p.ProductImages.FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                                   ?? p.ProductImages.FirstOrDefault()?.ImageUrl;
                    return new ProductBestSellerDto
                    {
                        ProductId = id,
                        ProductName = p.ProductName,
                        Unit = p.Unit,
                        ImageUrl = imageUrl,
                        SoldQuantity = soldByProductId[id]
                    };
                })
                .ToList();
        }

        public async Task<decimal> GetSoldQuantityAsync(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                return 0m;
            }

            var deliveredOrderIds = _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.Status != null && o.Status.ToLower() == "delivered")
                .Select(o => o.OrderId);

            // Variant lines -> sold by quantity
            var soldFromVariants = await _context.Set<OrderDetail>()
                .AsNoTracking()
                .Where(d => deliveredOrderIds.Contains(d.OrderId) && d.VariantId.HasValue)
                .Join(
                    _context.Set<ProductVariant>().AsNoTracking(),
                    d => d.VariantId!.Value,
                    v => v.VariantId,
                    (d, v) => new { v.ProductId, Qty = (decimal)d.Quantity })
                .Where(x => x.ProductId == productId)
                .SumAsync(x => (decimal?)x.Qty) ?? 0m;

            // Combo lines -> expand MealComboItems for just this product
            var soldFromCombosRaw = await _context.Set<OrderDetail>()
                .AsNoTracking()
                .Where(d => deliveredOrderIds.Contains(d.OrderId) && d.MealComboId.HasValue)
                .Select(d => new { MealComboId = d.MealComboId!.Value, ComboQty = d.Quantity })
                .ToListAsync();

            decimal soldFromCombos = 0m;
            if (soldFromCombosRaw.Count > 0)
            {
                var comboIds = soldFromCombosRaw.Select(x => x.MealComboId).Distinct().ToList();
                var items = await _context.Set<MealComboItem>()
                    .AsNoTracking()
                    .Where(i => comboIds.Contains(i.MealComboId) && i.ProductId == productId)
                    .Select(i => new { i.MealComboId, i.Quantity })
                    .ToListAsync();

                var qtyByComboId = items
                    .GroupBy(x => x.MealComboId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                foreach (var line in soldFromCombosRaw)
                {
                    if (!qtyByComboId.TryGetValue(line.MealComboId, out var itemQty)) continue;
                    var comboQty = Math.Max(1, line.ComboQty);
                    var add = comboQty * itemQty;
                    if (add > 0) soldFromCombos += add;
                }
            }

            return soldFromVariants + soldFromCombos;
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
