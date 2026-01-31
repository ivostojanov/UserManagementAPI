using UserManagementAPI.Models;
using UserManagementAPI.Stores;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/users", () => Results.Ok(InMemoryUserStore.GetAll()))
    .WithName("GetUsers")
    .WithOpenApi();

app.MapGet("/users/{id:int}", (int id) =>
{
    var user = InMemoryUserStore.Get(id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
})
    .WithName("GetUserById")
    .WithOpenApi();

app.MapPost("/users", (CreateUserRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { Error = "Name is required" });
    var created = InMemoryUserStore.Add(req.Name, req.Email);
    return Results.Created($"/users/{created.Id}", created);
})
    .WithName("CreateUser")
    .WithOpenApi();

app.MapPut("/users/{id:int}", (int id, CreateUserRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { Error = "Name is required" });
    var updated = InMemoryUserStore.Update(id, req.Name, req.Email);
    return updated ? Results.NoContent() : Results.NotFound();
})
    .WithName("UpdateUser")
    .WithOpenApi();

app.MapDelete("/users/{id:int}", (int id) =>
{
    var deleted = InMemoryUserStore.Delete(id);
    return deleted ? Results.NoContent() : Results.NotFound();
})
    .WithName("DeleteUser")
    .WithOpenApi();

app.Run();
