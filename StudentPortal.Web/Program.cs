using AutoMapper;
using AutoMapper.Extensions.Microsoft.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Register AutoMapper with the DI container
builder.Services.AddAutoMapper(typeof(MappingProfile)); // Make sure MappingProfile exists

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddControllersWithViews();

// Other services registrations...
