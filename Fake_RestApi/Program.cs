using Fake_RestApi.DataAccess;
using Fake_RestApi.Entities;
using Fake_RestApi.Repository;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Fake_RestApi.Dto;

namespace Fake_RestApi
{
    internal class Program
    {

        static async Task Main(string[] args)
        {
            TcpListener listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 27001);
            listener.Start();

            Console.WriteLine("Fake Rest Api Server started...");

            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                {
                    await HandleClientAsync(client);
                });
                Console.WriteLine("Yeni client qosuldu...");
            }

        }

        static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                using var stream = client.GetStream();
                var buffer = new byte[client.ReceiveBufferSize];
                var length = await stream.ReadAsync(buffer, 0, buffer.Length);
                var rawRequest = Encoding.UTF8.GetString(buffer, 0, length);
                Console.WriteLine(rawRequest);
                string response;
                try
                {
                    var request = HttpRequest.Parse(rawRequest);
                    response = await Router.HandleAsync(request);
                }
                catch (Exception ex)
                {
                    response = HttpResponse.Json(new { message = "Server error", error = ex.Message });
                }

                var responseBytes = Encoding.UTF8.GetBytes(response);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
            }
        }


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

        public class HttpResponse
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
                // 2-ci addim: her sorgu ucun yeni context ve repo
                using var context = new FakeApiDBContext();
                var repo = new ProductRepository(context);
                var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // GET /products  -> butun mehsullar
                if (request.Method == "GET" && request.Path == "/products")
                {
                    var products = await repo.GetAllProduct();
                    return HttpResponse.Json(products);
                }

                // GET /products/{id}
                if (request.Method == "GET" && request.Path!.StartsWith("/products/"))
                {
                    var idText = request.Path.Replace("/products/", ""); //yalniz id qalsin deye
                    if (!int.TryParse(idText, out var id)) //id casting olunmasa
                    {
                        return HttpResponse.Json(new { message = "Invalid product id" });
                    }
                    var product = await repo.GetProduct(id);
                    if (product is null)
                    {
                        return HttpResponse.Json(new { message = "Product not found" });
                    }

                    return HttpResponse.Json(product);
                }

                // POST /products  -> body: { "name": "...", "price": 10.5 }
                if (request.Method == "POST" && request.Path == "/products")
                {
                    CreateProductDto? dto;
                    try
                    {
                        dto = JsonSerializer.Deserialize<CreateProductDto>(request.Body!, jsonOptions);
                    }
                    catch (JsonException)
                    {
                        return HttpResponse.Json(new { message = "Invalid JSON body" });
                    }

                    if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                    {
                        return HttpResponse.Json(new { message = "Invalid product data" });
                    }

                    var newProduct = new Product
                    {
                        Name = dto.Name,
                        Price = dto.Price
                    };

                    var added = repo.Add(newProduct);
                    if (!added)
                    {
                        return HttpResponse.Json(new { message = "Product could not be added" });
                    }

                    return HttpResponse.Json(newProduct);
                }

                // PUT /products/{id}  -> body: { "name": "...", "price": 20 }
                if (request.Method == "PUT" && request.Path!.StartsWith("/products/"))
                {
                    var idText = request.Path.Replace("/products/", "");
                    if (!int.TryParse(idText, out var id))
                    {
                        return HttpResponse.Json(new { message = "Invalid product id" });
                    }

                    UpdateProductDto? dto;
                    try
                    {
                        dto = JsonSerializer.Deserialize<UpdateProductDto>(request.Body!, jsonOptions);
                    }
                    catch (JsonException)
                    {
                        return HttpResponse.Json(new { message = "Invalid JSON body" });
                    }

                    if (dto is null || string.IsNullOrWhiteSpace(dto.Name))
                    {
                        return HttpResponse.Json(new { message = "Invalid product data" });
                    }

                    var existing = await repo.GetProduct(id);
                    if (existing is null)
                    {
                        return HttpResponse.Json(new { message = "Product not found" });
                    }

                    // movcud (track olunan) obyekti deyisirik
                    existing.Name = dto.Name;
                    existing.Price = dto.Price;

                    var updated = repo.Update(existing);
                    if (!updated)
                    {
                        return HttpResponse.Json(new { message = "Product could not be updated" });
                    }

                    return HttpResponse.Json(existing);
                }

                // DELETE /products/{id}
                if (request.Method == "DELETE" && request.Path!.StartsWith("/products/"))
                {
                    var idText = request.Path.Replace("/products/", "");
                    if (!int.TryParse(idText, out var id))
                    {
                        return HttpResponse.Json(new { message = "Invalid product id" });
                    }

                    var existing = await repo.GetProduct(id);
                    if (existing is null)
                    {
                        return HttpResponse.Json(new { message = "Product not found" });
                    }

                    var deleted = repo.Delete(id);
                    if (!deleted)
                    {
                        return HttpResponse.Json(new { message = "Product could not be deleted" });
                    }

                    return HttpResponse.Json(new { message = $"Product {id} deleted" });
                }

                return HttpResponse.Json(new { message = "Endpoint not found" });
            }
        }

    }
}