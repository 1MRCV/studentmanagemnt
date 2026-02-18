using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Web.DataContext;
using StudentPortal.Web.Models;
using StudentPortal.Web.Models.Students.ResponceModels;

namespace StudentPortal.Web.Services.Students
{
    public class StudentService : IStudentService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IMapper _mapper;

        public StudentService(ApplicationDbContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<ServiceResponce<NewStudentViewModel>> CreateAsync(NewStudentViewModel newStudent)
        {
            var responce = new ServiceResponce<NewStudentViewModel>();
            var student = await GetByEmailAsync(newStudent.Email);
            if (student is not null)
            {
                responce.Data = newStudent;
                responce.Success = false;
                responce.ModelErrorField = nameof(ModelFields.Email);
                responce.ErrorMessage = "Student with the same email already exists";
                return responce;
            }

            var dbStudent = _mapper.Map<Student>(newStudent);
            await _dbContext.Students.AddAsync(dbStudent);
            await _dbContext.SaveChangesAsync();

            return responce;
        }

        public async Task DeleteAsync(int id)
        {
            var student = await GetByIdAsync(id);
            if (student is not null)
            {
                _dbContext.Students.Remove(student);
                await _dbContext.SaveChangesAsync();
            }
        }

        public async Task<List<StudentViewModel>> GetAsync()
        {
            var students = await _dbContext.Students.AsNoTracking().ToListAsync();
            return _mapper.Map<List<StudentViewModel>>(students);
        }

        public async Task<Student?> GetByIdAsync(int id)
        {
            return await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Student?> GetByEmailAsync(string email)
        {
            return await _dbContext.Students.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<ServiceResponce<UpdateStudentViewModel>> UpdateAsync(UpdateStudentViewModel updateStudent)
        {
            var responce = new ServiceResponce<UpdateStudentViewModel>();
            var student = await GetByEmailAsync(updateStudent.Email);

            // ✅ Compare int Id instead of Guid
            if (student is not null && updateStudent.Id != student.Id)
            {
                responce.Data = updateStudent;
                responce.Success = false;
                responce.ModelErrorField = nameof(ModelFields.Email);
                responce.ErrorMessage = "Student with the same email already exists";
                return responce;
            }

            // Map and update
            var dbStudent = _mapper.Map<Student>(updateStudent);
            _dbContext.Students.Update(dbStudent);
            await _dbContext.SaveChangesAsync();

            return responce;
        }
    }
}
