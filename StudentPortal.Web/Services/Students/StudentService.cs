using AutoMapper;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Web.DataContext;
using StudentPortal.Web.Models;
using StudentPortal.Web.Models.Students.DTOs;

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

        public async Task<ServiceResponse<NewStudentViewModel>> CreateAsync(NewStudentViewModel newStudent)
        {
            var response = new ServiceResponse<NewStudentViewModel>();

            var student = await GetByEmailAsync(newStudent.Email);
            if (student is not null)
            {
                response.Data = newStudent;
                response.Success = false;
                response.ModelErrorField = nameof(ModelFields.Email);
                response.ErrorMessage = "Student with the same email already exists";
                return response;
            }

            var dbStudent = _mapper.Map<Student>(newStudent);
            await _dbContext.Students.AddAsync(dbStudent);
            await _dbContext.SaveChangesAsync();

            return response;
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
            var students = await _dbContext.Students
                                           .AsNoTracking()
                                           .ToListAsync();

            return _mapper.Map<List<StudentViewModel>>(students);
        }

        public async Task<Student?> GetByIdAsync(int id)
        {
            return await _dbContext.Students
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<Student?> GetByEmailAsync(string email)
        {
            return await _dbContext.Students
                                   .AsNoTracking()
                                   .FirstOrDefaultAsync(x => x.Email == email);
        }

        public async Task<ServiceResponse<UpdateStudentViewModel>> UpdateAsync(UpdateStudentViewModel updateStudent)
        {
            var response = new ServiceResponse<UpdateStudentViewModel>();

            var student = await GetByEmailAsync(updateStudent.Email);

            if (student is not null && updateStudent.Id != student.Id)
            {
                response.Data = updateStudent;
                response.Success = false;
                response.ModelErrorField = nameof(ModelFields.Email);
                response.ErrorMessage = "Student with the same email already exists";
                return response;
            }

            var dbStudent = _mapper.Map<Student>(updateStudent);
            _dbContext.Students.Update(dbStudent);
            await _dbContext.SaveChangesAsync();

            return response;
        }
    }
}