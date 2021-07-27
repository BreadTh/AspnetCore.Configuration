namespace BreadTh.AspNet.Configuration
{
    public record EnvironmentString(string Value)
    {
        public bool IsProduction() => Value == "Production";
        public bool IsStaging() => Value == "Staging";
        public bool IsDevelopment() => Value == "Development";
    }
}
