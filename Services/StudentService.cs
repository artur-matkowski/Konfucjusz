
public class StudentService
{
    protected readonly ApplicationDbContext _dbcontext;

    public StudentService(ApplicationDbContext dbcontext)
    {
        _dbcontext = dbcontext;
    }
    public List<StudentClass> GetStudents()
    {
        return _dbcontext.student.ToList();
    }
    
    public void AddStudent(StudentClass _student)
    {
        _dbcontext.student.Add(_student);
        _dbcontext.SaveChanges();
    }
}