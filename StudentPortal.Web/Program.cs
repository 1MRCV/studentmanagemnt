using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Web.DataContext;
using StudentPortal.Web.Helpers;
using StudentPortal.Web.Services.Students;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutoMapper(typeof(MappingProfile));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"));
});

// 🔥 ADD THIS LINE
builder.Services.AddScoped<IStudentService, StudentService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Student}/{action=Logbook}/{id?}");

app.Run();