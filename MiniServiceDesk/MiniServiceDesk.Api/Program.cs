using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiniServiceDesk.Api.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var cs = builder.Configuration["ConnectionStrings:Default"];
builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlite(cs));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .WithOrigins("https://localhost:7044", "http://localhost:5044"));
});


var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();
app.UseCors("AllowWeb");

app.MapControllers();

app.Run();
