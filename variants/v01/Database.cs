using MySqlConnector;
using System.Data;
using System.Text.Json;
public static class Database
{
    public static string Name = "pp05_demo";
    public static MySqlConnection Open(bool admin=false)
    {
        var cs=Environment.GetEnvironmentVariable(admin?"PP05_ADMIN_DB":"PP05_DB") ??
            $"Server=127.0.0.1;Port=33405;Database={Name};User ID={(admin?"root":"pp05_app")};Password={(admin?"Practice05-local":"Practice05-app")};Allow User Variables=true";
        // Commands execute inside the connection's active transaction.
        var builder=new MySqlConnectionStringBuilder(cs){IgnoreCommandTransaction=true};
        var c=new MySqlConnection(builder.ConnectionString);c.Open();return c;
    }
    public static MySqlCommand Cmd(MySqlConnection c,string sql,params (string,object?)[] args)
    {
        var cmd=new MySqlCommand(sql,c);
        foreach(var (key,val) in args)cmd.Parameters.AddWithValue(key,val??DBNull.Value);
        return cmd;
    }
    public static List<Dictionary<string,object?>> Rows(MySqlConnection c,string sql,params (string,object?)[] args)
    {
        using var cmd=Cmd(c,sql,args);using var r=cmd.ExecuteReader();var rows=new List<Dictionary<string,object?>>();
        while(r.Read()){var d=new Dictionary<string,object?>();for(int i=0;i<r.FieldCount;i++)d[r.GetName(i)]=r.IsDBNull(i)?null:r.GetValue(i);rows.Add(d);}return rows;
    }
    public static void Exec(MySqlConnection c,string sql,params (string,object?)[] args){using var cmd=Cmd(c,sql,args);cmd.ExecuteNonQuery();}
    public static void Script(MySqlConnection c,string path)
    {
        string delimiter=";",buffer="";
        foreach(var line in File.ReadLines(path))
        {
            if(line.StartsWith("DELIMITER ")){delimiter=line[10..].Trim();continue;}
            buffer+=line+"\n";
            if(buffer.TrimEnd().EndsWith(delimiter))
            {var sql=buffer.TrimEnd()[..^delimiter.Length].Trim();if(sql.Length>0)Exec(c,sql);buffer="";}
        }
        if(buffer.Trim().Length>0)throw new Exception("Незавершённая SQL-команда");
    }
    public static void Init(JsonElement data)
    {
        // This command only prepares a new teaching database. It never drops an existing one.
        using(var server=new MySqlConnection(Environment.GetEnvironmentVariable("PP05_SERVER_DB")??"Server=127.0.0.1;Port=33405;User ID=root;Password=Practice05-local"))
        {server.Open();Exec(server,$"CREATE DATABASE IF NOT EXISTS `{Name}` CHARACTER SET utf8mb4");}
        using var c=Open(true);
        Script(c,"schema.sql");
        if(Rows(c,"SELECT id FROM products LIMIT 1").Count>0)throw new Exception("База уже заполнена. Инициализация отменена; для продолжения использовать dotnet run.");
        using var tx=c.BeginTransaction();
        try
        {
            foreach(var p in data.GetProperty("products").EnumerateArray())
                Exec(c,"INSERT INTO products(id,code,name) VALUES(@id,@code,@name)",("@id",p.GetProperty("id").GetInt32()),("@code",p.GetProperty("code").GetString()),("@name",p.GetProperty("name").GetString()));
            foreach(var p in data.GetProperty("materials").EnumerateArray())
                Exec(c,"INSERT INTO materials(id,name,unit,price) VALUES(@id,@name,@unit,@price)",("@id",p.GetProperty("id").GetInt32()),("@name",p.GetProperty("name").GetString()),("@unit",p.GetProperty("unit").GetString()),("@price",p.GetProperty("price").GetDecimal()));
            foreach(var p in data.GetProperty("operations").EnumerateArray())
                Exec(c,"INSERT INTO operations(id,name,rate_hour) VALUES(@id,@name,@price)",("@id",p.GetProperty("id").GetInt32()),("@name",p.GetProperty("name").GetString()),("@price",p.GetProperty("rate").GetDecimal()));
            foreach(var p in data.GetProperty("material_norms").EnumerateArray())
                Exec(c,"INSERT INTO material_norms(product_id,material_id,norm) VALUES(@p,@m,@n)",("@p",p.GetProperty("product").GetInt32()),("@m",p.GetProperty("material").GetInt32()),("@n",p.GetProperty("norm").GetDecimal()));
            foreach(var p in data.GetProperty("operation_norms").EnumerateArray())
                Exec(c,"INSERT INTO operation_norms(product_id,operation_id,hours) VALUES(@p,@o,@h)",("@p",p.GetProperty("product").GetInt32()),("@o",p.GetProperty("operation").GetInt32()),("@h",p.GetProperty("hours").GetDecimal()));
            ImportCustomers(c,"customers.json");
            SeedAccount(c,"admin","Admin-05!","Учебный администратор","Administrator");
            SeedAccount(c,"student","Student-05!","Учебный пользователь","User");
            tx.Commit();
        }
        catch{tx.Rollback();throw;}
        Script(c,"objects.sql");
        Exec(c,"CREATE USER IF NOT EXISTS 'pp05_app'@'%' IDENTIFIED BY 'Practice05-app'");
        Exec(c,$"GRANT SELECT, INSERT, UPDATE, DELETE, EXECUTE ON `{Name}`.* TO 'pp05_app'@'%'");
        foreach(var (role,grant) in new[]{("pp05_read","SELECT"),("pp05_write","SELECT, INSERT, UPDATE, DELETE, EXECUTE"),("pp05_admin","ALL PRIVILEGES")})
        {Exec(c,$"CREATE ROLE IF NOT EXISTS '{role}'");Exec(c,$"GRANT {grant} ON `{Name}`.* TO '{role}'");}
        foreach(var (user,role) in new[]{("pp05_reader","pp05_read"),("pp05_writer","pp05_write"),("pp05_owner","pp05_admin")})
        {Exec(c,$"CREATE USER IF NOT EXISTS '{user}'@'%' IDENTIFIED BY 'Practice05-role'");Exec(c,$"GRANT '{role}' TO '{user}'@'%'");Exec(c,$"SET DEFAULT ROLE ALL TO '{user}'@'%'");}
        Console.WriteLine($"Создана {Name}. Заказчиков: {Rows(c,"SELECT id FROM customers").Count}. Повторный импорт не создаёт дубликатов.");
    }
    static void SeedAccount(MySqlConnection c,string login,string password,string name,string role)
    {
        Exec(c,"INSERT INTO users(login,password_hash,full_name,role) VALUES(@l,@h,@n,@r)",("@l",login),("@h",Users.Hash(password)),("@n",name),("@r",role));
    }
    public static int ImportCustomers(MySqlConnection c,string path)
    {
        var items=JsonDocument.Parse(File.ReadAllText(path)).RootElement;int count=0;
        foreach(var p in items.EnumerateArray())
        {
            // Source spelling addres is deliberately mapped to the address column.
            Exec(c,"INSERT IGNORE INTO customers(id,name,inn,address,phone,kind) VALUES(@id,@name,@inn,@address,@phone,@kind)",
                ("@id",p.GetProperty("id").GetString()),("@name",p.GetProperty("name").GetString()),("@inn",p.GetProperty("inn").GetString()),
                ("@address",p.GetProperty("addres").GetString()),("@phone",p.GetProperty("phone").GetString()),("@kind",p.GetProperty("type").GetString()));count++;
        }return count;
    }
}
