using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.EntityFrameworkCore;
using OptimiseApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddProblemDetails();

var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseExceptionHandler();

app.MapGet("/api/products", async (string? code, string? partOfDescription, IConfiguration configuration, CancellationToken cancellationToken) => {
    var connectionString = configuration.GetConnectionString("OptimiseDb");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return Results.Problem(
            title: "Database connection is not configured.",
            detail: "Set environment variable 'ConnectionStrings__OptimiseDb'.",
            statusCode: StatusCodes.Status500InternalServerError);
    }

    var products = new List<ProductDto>();

    try
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand("usp_GetProductSearch", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@Code", string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim());
        command.Parameters.AddWithValue("@PartOfDescription", string.IsNullOrWhiteSpace(partOfDescription) ? string.Empty : partOfDescription.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            products.Add(new ProductDto(
                Code: reader["Code"]?.ToString() ?? string.Empty,
                Description: reader["Description"]?.ToString() ?? string.Empty,
                Model: reader["Model"]?.ToString() ?? string.Empty,
                ProductGroup: reader["ProductGroup"]?.ToString() ?? string.Empty,
                StockLevel: reader["StockLevel"] is DBNull ? 0 : Convert.ToInt32(reader["StockLevel"])));
        }

        return Results.Ok(products);
    }
    catch (SqlException ex)
    {
        return Results.Problem(
            title: "Database query failed.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
})
.WithName("GetProducts")
.WithOpenApi();

app.Run();

public record ProductDto(
    string Code,
    string Description,
    string Model,
    string ProductGroup,
    int StockLevel
);
