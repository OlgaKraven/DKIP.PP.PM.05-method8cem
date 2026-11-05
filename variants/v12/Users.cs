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
        // TODO: реализовать по разбору методики.
        throw new NotImplementedException();
    }
    public static void Edit(MySqlConnection c,int id,UserInput u)
    {
        // TODO: реализовать по разбору методики.
        throw new NotImplementedException();
    }
    public static LoginResult Login(MySqlConnection c,LoginInput u,bool puzzleOk)
    {
        // TODO: реализовать по разбору методики.
        throw new NotImplementedException();
    }
}
