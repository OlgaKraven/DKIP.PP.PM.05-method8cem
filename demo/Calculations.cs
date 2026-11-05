using MySqlConnector;
public static class Calculations
{
    public static decimal OrderTotal(MySqlConnection c,int orderId)
    {
        using var cmd=Database.Cmd(c,"SELECT ROUND(SUM(l.quantity*(v.material_cost+v.operation_cost)),2) FROM order_lines l JOIN v_product_cost v ON v.id=l.product_id WHERE l.order_id=@id",("@id",orderId));
        object? value=cmd.ExecuteScalar();
        return value is null or DBNull?0:Convert.ToDecimal(value);
    }
}
