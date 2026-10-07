namespace config;

public class ProcessamentoConfig
{
    public int GrauConcorrencia { get; set; } = 10;
    public int TotalPedidos { get; set; } = 1000;
    public double TaxaErro { get; set; } = 0.1;
}
