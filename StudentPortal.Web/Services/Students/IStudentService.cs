using StudentPortal.Web.Models;
using StudentPortal.Web.Models.Students.ResponceModels;

namespace StudentPortal.Web.Services.Students
{
    public interface IStudentService
    {
        Task<ServiceResponce<NewStudentViewModel>> CreateAsync(NewStudentViewModel newStudent);
        Task<ServiceResponce<UpdateStudentViewModel>> UpdateAsync(UpdateStudentViewModel newStudent);
        Task DeleteAsync(int id);                     // changed from int to int
        Task<Student?> GetByIdAsync(int id);         // changed from int to int
        Task<Student?> GetByEmailAsync(string email); // corrected parameter name for clarity
        Task<List<StudentViewModel>> GetAsync();
    }
}
