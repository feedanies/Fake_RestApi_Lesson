using System;
using System.Collections.Generic;
using System.Text;

namespace Fake_RestApi.Dto
{
    public class CreateProductDto
    {
        public string? Name { get; set; }
        public decimal Price { get; set; }
    }
}
