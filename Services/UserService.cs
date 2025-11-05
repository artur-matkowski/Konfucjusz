
public class UserService
{
    protected readonly ApplicationDbContext _dbcontext;

    public UserService(ApplicationDbContext dbcontext)
    {
        _dbcontext = dbcontext;
    }
    public List<UserAccount> GetUsers()
    {
        return _dbcontext.users.ToList();
    }

    public void Add(UserAccount _student)
    {
        _dbcontext.users.Add(_student);
        _dbcontext.SaveChanges();
    }

    public void Update(UserAccount user)
    {
        var existing = _dbcontext.users.Find(user.Id);
        if (existing is null) return;
        existing.userName = user.userName;
        existing.surname = user.surname;
        existing.userEmail = user.userEmail;
        existing.userRole = user.userRole;
        existing.mailValidated = user.mailValidated;
        existing.userCreationConfirmedByAdmin = user.userCreationConfirmedByAdmin;
        _dbcontext.SaveChanges();
    }
    
    public void Remove(string? mail)
    {
        if (mail == null) return;
        
        var user = _dbcontext.users.Where(u => u.userEmail == mail).FirstOrDefault();
        if(user == null) return;
        _dbcontext.users.Remove(user);
        _dbcontext.SaveChanges();
    }
}