namespace pedido;

public class Pedido
{
    public int Id { get; set; }
    public decimal Valor { get; set; }
    public string Cliente { get; set; } = string.Empty;
}

public class PedidoComErro
{
    public Pedido Pedido { get; set; } = new();
    public string Motivo { get; set; } = string.Empty;
}

public class PedidoException : Exception
{
    public PedidoException(string motivo) : base(motivo) { }
}
