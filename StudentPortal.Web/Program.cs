global using Microsoft.EntityFrameworkCore;
global using Microsoft.AspNetCore.Mvc;
global using StudentPortal.Web.Models.Students.DTOs;
global using StudentPortal.Web.Models.Students;
global using StudentPortal.Web.Models.Students.ResponceModels;
using StudentPortal.Web.DataContext;
using StudentPortal.Web.Helpers;
using StudentPortal.Web.Services.Students;
using AutoMapper;  // Make sure AutoMapper is properly referenced

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext with SQL Server configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
});

// Register AutoMapper using a Profile class (make sure MappingProfile exists)
builder.Services.AddAutoMapper(typeof(MappingProfile));  // Ensure MappingProfile class exists

// Register services with DI container
builder.Services.AddScoped<IStudentService, StudentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Configure route to be used by the app
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Student}/{action=Logbook}/{id?}");

app.Run();
