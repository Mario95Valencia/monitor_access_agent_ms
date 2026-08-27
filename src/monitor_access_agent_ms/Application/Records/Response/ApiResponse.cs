namespace monitor_access_agent_ms.Application.Records.Response
{

        //Record generico para las respuestas de la API del cliente
        public record ApiResponse<T>
        {
            public Guid Id { get; init; } = new();
            public string Type { get; init; } = string.Empty;
            public T? Data { get; init; } 
            public string? Message { get; init; } = string.Empty;

            public ApiResponse(Guid Id, string Type, T? Data, string? Message)
            {
                this.Id = Id;
                this.Type = Type;
                this.Data = Data;
                this.Message = Message;
            }
            public ApiResponse() { }
        }
}
