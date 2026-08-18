using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

// =====================================================
// 1. CONFIGURAÇÃO
// =====================================================

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("A variável OPENAI_API_KEY não foi configurada.");
    return;
}

using HttpClient httpClient = new();

httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
httpClient.DefaultRequestHeaders.Authorization =
    new AuthenticationHeaderValue("Bearer", apiKey);

Console.Write("Digite sua pergunta: ");

string pergunta = Console.ReadLine()
    ?? "Qual é o saldo do cliente 101?";

// =====================================================
// 2. DESCRIÇÃO DA FUNÇÃO PARA A IA
// =====================================================

var ferramenta = new
{
    type = "function",

    name = "consultar_saldo_cliente",

    description =
        "Consulta o saldo financeiro de um cliente usando seu código numérico.",

    parameters = new
    {
        type = "object",

        properties = new
        {
            clienteId = new
            {
                type = "integer",
                description = "Código numérico do cliente."
            }
        },

        required = new[] { "clienteId" },

        // Impede a IA de enviar propriedades não declaradas.
        additionalProperties = false
    },

    // Exige que os argumentos respeitem o schema.
    strict = true
};

// =====================================================
// 3. PRIMEIRA REQUISIÇÃO
// =====================================================

var primeiraRequisicao = new
{
    model = "gpt-5.6",

    instructions = """
        Você é um assistente financeiro.
        Para consultar saldos, utilize a ferramenta disponível.
        Nunca invente saldos ou códigos de clientes.
        """,

    input = pergunta,

    tools = new[] { ferramenta },

    // Permite no máximo uma chamada por vez neste exemplo.
    parallel_tool_calls = false
};

HttpResponseMessage primeiraRespostaHttp =
    await httpClient.PostAsJsonAsync("responses", primeiraRequisicao);

string primeiroJson =
    await primeiraRespostaHttp.Content.ReadAsStringAsync();

if (!primeiraRespostaHttp.IsSuccessStatusCode)
{
    Console.WriteLine("Erro na primeira chamada:");
    Console.WriteLine(primeiroJson);
    return;
}

JsonNode primeiraResposta = JsonNode.Parse(primeiroJson)!;

string responseId = primeiraResposta["id"]?.GetValue<string>()
    ?? throw new InvalidOperationException(
        "A resposta da API não possui um ID.");

// =====================================================
// 4. LOCALIZAR A SOLICITAÇÃO DE FUNÇÃO
// =====================================================

JsonNode? chamadaFuncao = primeiraResposta["output"]?
    .AsArray()
    .FirstOrDefault(item =>
        item?["type"]?.GetValue<string>() == "function_call");

if (chamadaFuncao is null)
{
    string textoDireto = ObterTextoResposta(primeiraResposta);

    Console.WriteLine();
    Console.WriteLine($"IA: {textoDireto}");
    return;
}

string callId = chamadaFuncao["call_id"]?.GetValue<string>()
    ?? throw new InvalidOperationException(
        "A chamada não possui call_id.");

string nomeFuncao = chamadaFuncao["name"]?.GetValue<string>()
    ?? throw new InvalidOperationException(
        "A chamada não possui nome.");

string argumentosJson = chamadaFuncao["arguments"]?.GetValue<string>()
    ?? "{}";

Console.WriteLine();
Console.WriteLine($"Função solicitada: {nomeFuncao}");
Console.WriteLine($"Argumentos recebidos: {argumentosJson}");

// =====================================================
// 5. VALIDAR A FUNÇÃO E OS ARGUMENTOS
// =====================================================

// Lista explícita das funções permitidas.
if (nomeFuncao != "consultar_saldo_cliente")
{
    Console.WriteLine("A IA solicitou uma função não autorizada.");
    return;
}

ConsultarSaldoArgumentos? argumentos;

try
{
    argumentos = JsonSerializer.Deserialize<ConsultarSaldoArgumentos>(
        argumentosJson,
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
}
catch (JsonException)
{
    Console.WriteLine("Os argumentos enviados pela IA são inválidos.");
    return;
}

if (argumentos is null ||
    argumentos.ClienteId < 1 ||
    argumentos.ClienteId > 9999)
{
    Console.WriteLine("O código do cliente está fora do intervalo permitido.");
    return;
}

// =====================================================
// 6. EXECUTAR A FUNÇÃO NO BACKEND
// =====================================================

ResultadoSaldo resultado =
    ConsultarSaldoCliente(argumentos.ClienteId);

string resultadoJson = JsonSerializer.Serialize(resultado);

Console.WriteLine($"Resultado do backend: {resultadoJson}");

// =====================================================
// 7. DEVOLVER O RESULTADO PARA A IA
// =====================================================

var segundaRequisicao = new
{
    model = "gpt-5.6",

    previous_response_id = responseId,

    input = new object[]
    {
        new
        {
            type = "function_call_output",
            call_id = callId,
            output = resultadoJson
        }
    },

    tools = new[] { ferramenta },

    parallel_tool_calls = false
};

HttpResponseMessage segundaRespostaHttp =
    await httpClient.PostAsJsonAsync("responses", segundaRequisicao);

string segundoJson =
    await segundaRespostaHttp.Content.ReadAsStringAsync();

if (!segundaRespostaHttp.IsSuccessStatusCode)
{
    Console.WriteLine("Erro na segunda chamada:");
    Console.WriteLine(segundoJson);
    return;
}

JsonNode segundaResposta = JsonNode.Parse(segundoJson)!;

Console.WriteLine();
Console.WriteLine($"IA: {ObterTextoResposta(segundaResposta)}");

// =====================================================
// FUNÇÃO REAL DO BACKEND
// =====================================================

static ResultadoSaldo ConsultarSaldoCliente(int clienteId)
{
    // Simulação de dados vindos de um banco.
    Dictionary<int, decimal> saldos = new()
    {
        [101] = 1_250.50m,
        [102] = -320.75m,
        [103] = 5_000.00m
    };

    if (!saldos.TryGetValue(clienteId, out decimal saldo))
    {
        return new ResultadoSaldo(
            Sucesso: false,
            ClienteId: clienteId,
            Saldo: null,
            Mensagem: "Cliente não encontrado."
        );
    }

    return new ResultadoSaldo(
        Sucesso: true,
        ClienteId: clienteId,
        Saldo: saldo,
        Mensagem: "Saldo consultado com sucesso."
    );
}

static string ObterTextoResposta(JsonNode resposta)
{
    JsonArray? output = resposta["output"]?.AsArray();

    if (output is null)
        return "A IA não retornou uma resposta.";

    foreach (JsonNode? item in output)
    {
        if (item?["type"]?.GetValue<string>() != "message")
            continue;

        JsonArray? content = item["content"]?.AsArray();

        if (content is null)
            continue;

        foreach (JsonNode? conteudo in content)
        {
            if (conteudo?["type"]?.GetValue<string>() == "output_text")
            {
                return conteudo["text"]?.GetValue<string>()
                    ?? "Resposta vazia.";
            }
        }
    }

    return "A IA não retornou texto.";
}

public sealed record ConsultarSaldoArgumentos(int ClienteId);

public sealed record ResultadoSaldo(
    bool Sucesso,
    int ClienteId,
    decimal? Saldo,
    string Mensagem
);