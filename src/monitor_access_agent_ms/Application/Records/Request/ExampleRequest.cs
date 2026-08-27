using System.Text.Json.Serialization;

namespace monitor_access_agent_ms.Application.Records.Request
{
    //Crear los Request con su constructor para poder enviar los datos como un JSON siempre
    public record ExampleRequest
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        //POR EJEMPLO, SI AQUI NO SE NECESITA ENVIAR EL STATUS AL USUARIO NO SE COLOCA      
        [JsonPropertyName("status")]
        public bool Status { get; set; } = true;


        public ExampleRequest() { }
        public ExampleRequest(long Id, string Name, bool Status)
        {
            this.Id = Id;
            this.Name = Name.Trim();
            this.Status = Status;
        }
    }

}
