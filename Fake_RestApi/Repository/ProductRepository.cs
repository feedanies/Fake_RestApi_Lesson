using Fake_RestApi.DataAccess;
using Fake_RestApi.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Fake_RestApi.Repository
{
    public class ProductRepository
    {
        private readonly FakeApiDBContext _dbContext;

        public ProductRepository(FakeApiDBContext dbContext)
        {
            _dbContext = dbContext;
        }


        public async Task<List<Product>> GetAllProduct()
        {
            return await _dbContext.Products.ToListAsync();
        }

        public async Task<Product?> GetProduct(int id)
        {
            return await _dbContext.Products.SingleOrDefaultAsync(p => p.Id == id);
        }

        public bool Add(Product product)
        {
            _dbContext.Products.Add(product);
            var result = _dbContext.SaveChanges();
            return result > 0;
        }

        public bool Delete(int id)
        {
            var product = _dbContext.Products.SingleOrDefault(p => p.Id == id);
            if (product != null)
            {
                _dbContext.Products.Remove(product);
            }
            var result = _dbContext.SaveChanges();
            return result > 0;
        }


        public bool Update(Product product)
        {
            _dbContext.Products.Update(product);
            var result = _dbContext.SaveChanges();
            return result > 0;
        }

    }
}
