using Fake_RestApi.DataAccess;
using Fake_RestApi.Dto;
using Fake_RestApi.Entities;
using Fake_RestApi.Repository;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Fake_RestApi
{
    public class HttpRequest
    {
        public string? Method { get; set; }
        public string? Path { get; set; }
        public string? Body { get; set; }

        public static HttpRequest Parse(string raw)
        {

            var lines = raw.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            var firstLine = lines[0].Trim();
            var parts = firstLine.Split(' ');

            var body = lines.Length > 1 ? string.Join('\n', lines.Skip(1)) : "";

            return new HttpRequest
            {
                Method = parts[0].Trim().ToUpper(),
                Path = parts[1].Trim(),
                Body = body.Trim(),
            };
        }
    }

    public static class HttpResponse
    {
        public static string Json(object data)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public static class Router
    {
        public static async Task<string> HandleAsync(HttpRequest request)
        {
            var context = new FakeApiDBContext();
            var repo = new ProductRepository(context);

            if (request.Method == "GET" && request.Path == "/products")
            {
                var products = await repo.GetAllProduct();
                return HttpResponse.Json(products);
            }
            else if (request.Method == "GET" && request.Path.StartsWith("/products/"))
            {
                var idText = request.Path.Replace("/products/", ""); //sadece id parcasini goture bilsin deye
                if (!int.TryParse(idText, out var id))
                {
                    return HttpResponse.Json(new { message = "Invalid product id.." });
                }
                var product = await repo.GetProduct(id);
                if (product is null)
                {
                    return HttpResponse.Json(new { message = "Product not found.." });
                }
                return HttpResponse.Json(product);
            }
            else if (request.Method == "POST" && request.Path == "/products")
            {
                var dto = JsonSerializer.Deserialize<CreateProductDto>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                {
                    return HttpResponse.Json(new { message = "Invalid product data.." });
                }
                var product = new Product
                {
                    Name = dto.Name,
                    Price = dto.Price
                };
                repo.Add(product);
                return HttpResponse.Json(product);
            }
            else if (request.Method == "PUT" && request.Path.StartsWith("/products/"))
            {
                var idText = request.Path.Replace("/products/", ""); //sadece id parcasini goture bilsin deye
                if (!int.TryParse(idText, out var id))
                {
                    return HttpResponse.Json(new { message = "Invalid product id.." });
                }

                var dto = JsonSerializer.Deserialize<UpdateProductDto>(request.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                {
                    return HttpResponse.Json(new { message = "Invalid product data.." });
                }

                var product = await repo.GetProduct(id);
                if (product is null)
                {
                    return HttpResponse.Json(new { message = "Product not found.." });
                }

                product.Name = dto.Name;
                product.Price = dto.Price;
                repo.Update(product);
                return HttpResponse.Json(product);
            }
            else if (request.Method == "DELETE" && request.Path.StartsWith("/products/"))
            {
                var idText = request.Path.Replace("/products/", ""); //sadece id parcasini goture bilsin deye
                if (!int.TryParse(idText, out var id))
                {
                    return HttpResponse.Json(new { message = "Invalid product id.." });
                }

                var product = await repo.GetProduct(id);
                if (product is null)
                {
                    return HttpResponse.Json(new { message = "Product not found.." });
                }

                repo.Delete(id);
                return HttpResponse.Json(new { message = "Product deleted.." });
            }

            return HttpResponse.Json(null);

        }
    }

    internal class Program
    {
        static async Task Main(string[] args)
        {
            FakeApiDBContext context = new FakeApiDBContext();
            var repo = new ProductRepository(context);

            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 27001);
            listener.Start();

            Console.WriteLine("Fake Api Server started...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    await HandleClientAsync(client);
                });
            }
        }


        static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                await using (var stream = client.GetStream())
                {
                    //clientden gelen melumati byte-dan strige cevirir
                    var buffer = new byte[client.ReceiveBufferSize];
                    var count = await stream.ReadAsync(buffer);
                    var rawRequest = Encoding.UTF8.GetString(buffer, 0, count);

                    Console.WriteLine("---------------------REQUEST------------------");
                    Console.WriteLine(rawRequest); //misal   Post /products/ {"name":"", "price":2300}

                    var request = HttpRequest.Parse(rawRequest); //method=Post path=/products/ body={"name":"", "price":2300}
                    var response = await Router.HandleAsync(request); //method secilib repo-da cagrilacaq ve string json qaytaracaq

                    var bytes = Encoding.UTF8.GetBytes(response);
                    await stream.WriteAsync(bytes); //string data byte ve streamlerle cliente oturulur
                }
            }
        }
    }
}