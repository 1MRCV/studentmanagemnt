using StudentPortal.Web.Models;
using StudentPortal.Web.Models.Students.DTOs;

namespace StudentPortal.Web.Services.Students
{
    public interface IStudentService
    {
        Task<ServiceResponse<NewStudentViewModel>> CreateAsync(NewStudentViewModel newStudent);

        Task<ServiceResponse<UpdateStudentViewModel>> UpdateAsync(UpdateStudentViewModel updateStudent);

        Task DeleteAsync(int id);

        Task<Student?> GetByIdAsync(int id);

        Task<Student?> GetByEmailAsync(string email);

        Task<List<StudentViewModel>> GetAsync();
    }
}