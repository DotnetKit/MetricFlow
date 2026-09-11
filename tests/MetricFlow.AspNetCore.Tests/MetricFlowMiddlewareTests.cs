using System.Net;
using DotnetKit.MetricFlow.Tracker;
using DotnetKit.MetricFlow.Tracker.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DotnetKit.MetricFlow.AspNetCore.Tests
{
    public class MetricFlowMiddlewareTests
    {
        [Fact]
        public async Task Middleware_ShouldTrackRoutePattern_AndNotRawUrl()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("TestApi");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/items/{id}", (string id) => Results.Ok(new { Id = id }));
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act: send requests with different IDs
            var response1 = await client.GetAsync("/items/101");
            var response2 = await client.GetAsync("/items/202");

            // Assert
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            response2.StatusCode.Should().Be(HttpStatusCode.OK);

            // Must track route pattern, not raw URLs
            tracker.GetValues("/items/101").Should().BeNull();
            tracker.GetValues("/items/202").Should().BeNull();

            var snapshot = tracker.GetValues("/items/{id}");
            snapshot.Should().NotBeNull();
            snapshot!.InCount.Should().Be(2);
            snapshot.OutCount.Should().Be(2);
            snapshot.FailedCount.Should().Be(0);
        }

        [Fact]
        public async Task Middleware_ShouldTrackEndpointName_WhenConfigured()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow(options =>
                            {
                                options.NamingStrategy = MetricRouteNamingStrategy.EndpointName;
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/weather", () => "Sunny")
                                    .WithName("GetWeatherForecast");
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act
            var response = await client.GetAsync("/weather");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var snapshot = tracker.GetValues("GetWeatherForecast");
            snapshot.Should().NotBeNull();
            snapshot!.InCount.Should().Be(1);
            snapshot.OutCount.Should().Be(1);
        }

        [Fact]
        public async Task Middleware_ShouldMark4xxAnd5xx_AsFailed()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("TestApi");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/bad", () => Results.BadRequest("Invalid"));
                                endpoints.MapGet("/notfound", () => Results.NotFound());
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act
            var badResp = await client.GetAsync("/bad");
            var notFoundResp = await client.GetAsync("/notfound");

            // Assert
            badResp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            notFoundResp.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var badSnapshot = tracker.GetValues("/bad");
            badSnapshot.Should().NotBeNull();
            badSnapshot!.FailedCount.Should().Be(1);

            var notFoundSnapshot = tracker.GetValues("/notfound");
            notFoundSnapshot.Should().NotBeNull();
            notFoundSnapshot!.FailedCount.Should().Be(1);
        }

        [Fact]
        public async Task Middleware_ShouldTrackExceptions_AndRethrow()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("TestApi");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/crash", () =>
                                {
                                    throw new InvalidOperationException("Boom!");
                                });
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act & Assert
            var act = async () => await client.GetAsync("/crash");
            await act.Should().ThrowAsync<InvalidOperationException>();

            var snapshot = tracker.GetValues("/crash");
            snapshot.Should().NotBeNull();
            snapshot!.FailedCount.Should().Be(1);
            snapshot.InCount.Should().Be(1);
            snapshot.OutCount.Should().Be(1);
        }

        [Fact]
        public async Task Middleware_ShouldSkipExcludedPaths()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("TestApi", options =>
                            {
                                options.ExcludedPaths.Add("/custom-skip");
                                options.ExcludePathPrefixes.Add("/internal");
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/health", () => "OK");
                                endpoints.MapGet("/custom-skip", () => "Skipped");
                                endpoints.MapGet("/internal/ping", () => "Pong");
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act
            await client.GetAsync("/health");
            await client.GetAsync("/custom-skip");
            await client.GetAsync("/internal/ping");

            // Assert
            tracker.GetAllSnapshots().Should().BeEmpty();
        }

        [Fact]
        public async Task Middleware_ShouldEnrichTags_WithTenantId()
        {
            // Arrange
            Dictionary<string, string>? capturedTags = null;

            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("TestApi", options =>
                            {
                                options.EnrichTags = (tags, ctx) =>
                                {
                                    if (ctx.Request.Headers.TryGetValue("X-Tenant-ID", out var tenantId))
                                    {
                                        tags["tenant_id"] = tenantId!;
                                    }
                                    capturedTags = new Dictionary<string, string>(tags);
                                };
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/tenant-data", () => "data");
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var request = new HttpRequestMessage(HttpMethod.Get, "/tenant-data");
            request.Headers.Add("X-Tenant-ID", "AcmeCorp");

            // Act
            var response = await client.SendAsync(request);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            capturedTags.Should().NotBeNull();
            capturedTags.Should().ContainKey("tenant_id").WhoseValue.Should().Be("AcmeCorp");
            capturedTags.Should().ContainKey("http_method").WhoseValue.Should().Be("GET");
            capturedTags.Should().ContainKey("http_status").WhoseValue.Should().Be("200");
        }

        [Fact]
        public async Task MapMetricFlow_ShouldExposeMetricsEndpoint()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow("ExpositionApi");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGet("/hello", () => "Hello World");
                                endpoints.MapMetricFlow("/metrics");
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();

            // Act 1: make normal call
            await client.GetAsync("/hello");

            // Act 2: call metrics endpoint
            var metricsResponse = await client.GetAsync("/metrics");
            var content = await metricsResponse.Content.ReadAsStringAsync();

            // Assert
            metricsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            metricsResponse.Content.Headers.ContentType?.MediaType.Should().Be("text/plain");
            content.Should().Contain("ExpositionApi");
            content.Should().Contain("/hello");

            // /metrics itself should not be tracked
            content.Should().NotContain("/metrics [InCount:");
        }

        [Fact]
        public async Task Middleware_ShouldIncludeHttpMethodInMetricName_WhenConfigured()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddMetricFlow(options =>
                            {
                                options.IncludeHttpMethodInMetricName = true;
                            });
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapPost("/orders", () => "Created");
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act
            await client.PostAsync("/orders", null);

            // Assert
            var snapshot = tracker.GetValues("POST /orders");
            snapshot.Should().NotBeNull();
            snapshot!.InCount.Should().Be(1);
        }

        [Fact]
        public async Task Middleware_ShouldTrackControllerEndpoints()
        {
            // Arrange
            using var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services.AddRouting();
                            services.AddControllers().AddApplicationPart(typeof(TestProductsController).Assembly);
                            services.AddMetricFlow("ControllerApi");
                        })
                        .Configure(app =>
                        {
                            app.UseRouting();
                            app.UseMetricFlow();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapControllers();
                            });
                        });
                })
                .StartAsync();

            var client = host.GetTestClient();
            var tracker = host.Services.GetRequiredService<MetricTracker>();

            // Act
            var response = await client.GetAsync("/api/testproducts/999");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Controller route pattern should be tracked (normalized with leading /)
            var snapshot = tracker.GetValues("/api/TestProducts/{id}");
            snapshot.Should().NotBeNull();
            snapshot!.InCount.Should().Be(1);
            snapshot.OutCount.Should().Be(1);
            snapshot.FailedCount.Should().Be(0);
        }
    }

    [Microsoft.AspNetCore.Mvc.ApiController]
    [Microsoft.AspNetCore.Mvc.Route("api/[controller]")]
    public class TestProductsController : Microsoft.AspNetCore.Mvc.ControllerBase
    {
        [Microsoft.AspNetCore.Mvc.HttpGet("{id}")]
        public Microsoft.AspNetCore.Mvc.IActionResult GetById(int id) => Ok(new { ProductId = id });
    }
}

