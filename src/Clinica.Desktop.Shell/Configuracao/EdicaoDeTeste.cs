namespace Clinica.Desktop.Shell.Configuracao;

/// <summary>Ativado somente no pacote portátil de validação, por propriedade de compilação.</summary>
public static class EdicaoDeTeste
{
#if CLINICA_TESTE_LOCAL
    public static bool Ativa => true;
#else
    public static bool Ativa => false;
#endif
    public static string NomePasta=>Ativa?"ClinicaSemDor-Teste-PR245":"ClinicaSemDor";
    public static string VariavelConexao=>Ativa?"CLINICA_TESTE_CONNECTION":"ConnectionStrings__Clinica";
}
