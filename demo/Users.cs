using MySqlConnector;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
public record UserInput(string Login,string Password,string FullName,string Role,bool Blocked);
public record LoginInput(string Login,string Password,int[] Pieces);
public record LoginResult(bool Success,string Message,int? UserId=null,string? Role=null);
public static class Users
{
    public const string Wrong="Вы ввели неверный логин или пароль. Пожалуйста проверьте ещё раз введенные данные";
    public const string Blocked="Вы заблокированы. Обратитесь к администратору";
    public static string Hash(string password)
    {
        byte[] salt=RandomNumberGenerator.GetBytes(16);
        byte[] hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,100000,HashAlgorithmName.SHA256,32);
        return $"100000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password,string value)
    {
        var v=value.Split(':');var actual=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(v[1]),int.Parse(v[0]),HashAlgorithmName.SHA256,32);
        return CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(v[2]));
    }
    public static string Normalize(string login)=>login.Trim().ToLowerInvariant();
    static void Validate(UserInput u,bool passwordRequired)
    {
        if(!Regex.IsMatch(Normalize(u.Login),"^[a-z0-9_-]{4,64}$"))throw new ArgumentException("Логин: 4–64 латинских буквы, цифры, _ или -.");
        if((passwordRequired||u.Password.Length>0)&&u.Password.Length<8)throw new ArgumentException("Пароль: не менее 8 символов.");
        if(string.IsNullOrWhiteSpace(u.FullName)||u.FullName.Length>160)throw new ArgumentException("Заполнить имя (до 160 символов).");
        if(u.Role is not ("Administrator" or "User"))throw new ArgumentException("Неизвестная роль.");
    }
    public static int Add(MySqlConnection c,UserInput u)
    {
        Validate(u,true);
        Database.Exec(c,"INSERT INTO users(login,password_hash,full_name,role,blocked) VALUES(@login,@hash,@name,@role,@blocked)",("@login",Normalize(u.Login)),("@hash",Hash(u.Password)),("@name",u.FullName.Trim()),("@role",u.Role),("@blocked",u.Blocked));
        return Convert.ToInt32(Database.Rows(c,"SELECT LAST_INSERT_ID() id")[0]["id"]);
    }
    public static void Edit(MySqlConnection c,int id,UserInput u)
    {
        Validate(u,false);
        string change=u.Password.Length>0?",password_hash=@hash":"";
        Database.Exec(c,"UPDATE users SET login=@login,full_name=@name,role=@role,blocked=@blocked,failed_attempts=IF(@blocked,failed_attempts,0)"+change+" WHERE id=@id",("@id",id),("@login",Normalize(u.Login)),("@name",u.FullName.Trim()),("@role",u.Role),("@blocked",u.Blocked),("@hash",u.Password.Length>0?Hash(u.Password):""));
    }
    public static LoginResult Login(MySqlConnection c,LoginInput u,bool puzzleOk)
    {
        if(string.IsNullOrWhiteSpace(u.Login)||string.IsNullOrEmpty(u.Password))return new(false,"Заполнить логин и пароль. Попытка не засчитана.");
        using var tx=c.BeginTransaction();
        var rows=Database.Rows(c,"SELECT * FROM users WHERE login=@login FOR UPDATE",("@login",Normalize(u.Login)));
        if(rows.Count==0){tx.Commit();return new(false,Wrong);}
        var row=rows[0];int id=Convert.ToInt32(row["id"]);
        if(Convert.ToBoolean(row["blocked"])){tx.Commit();return new(false,Blocked);}
        bool ok=Verify(u.Password,(string)row["password_hash"]!)&&puzzleOk;
        if(ok){Database.Exec(c,"UPDATE users SET failed_attempts=0 WHERE id=@id",("@id",id));tx.Commit();return new(true,"Вы успешно авторизовались",id,(string)row["role"]!);}
        int failures=Convert.ToInt32(row["failed_attempts"])+1;
        Database.Exec(c,"UPDATE users SET failed_attempts=@n,blocked=@blocked WHERE id=@id",("@n",failures),("@blocked",failures>=3),("@id",id));tx.Commit();
        return new(false,failures>=3?Blocked:puzzleOk?Wrong:"Пазл собран неверно. Поменять фрагменты местами и повторить вход.");
    }
}
