using System;
using System.Collections.Generic;
using System.Text;

namespace Fake_RestApi.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
}
