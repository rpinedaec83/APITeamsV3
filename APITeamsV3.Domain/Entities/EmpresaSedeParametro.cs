namespace APITeamsV3.Domain.Entities
{
    public class EmpresaSedeParametro
    {
        public int IdEmpresa { get; set; }
        public int IdSede { get; set; }
        public int IdParametro { get; set; }
        public int? IdSistema { get; set; }
        public string? Nombre { get; set; }
        public string? Titulo { get; set; }
        public string? Descripcion { get; set; }
        public string? Valor { get; set; }
        public string? Valor2 { get; set; }
        public string? Valor3 { get; set; }
        public string? Valor4 { get; set; }
        public string? Tabla { get; set; }
        public bool? Activo { get; set; }
        public string? Valor5 { get; set; }
    }
}
