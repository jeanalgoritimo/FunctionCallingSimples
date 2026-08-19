FunctionCallingSimples — Function Calling com C# e OpenAI






Exemplo educacional de Function Calling utilizando C#, .NET 9 e a Responses API da OpenAI. A aplicação recebe uma pergunta em linguagem natural, permite que o modelo solicite uma função previamente definida, valida essa solicitação no backend, consulta um saldo simulado e devolve o resultado para a IA produzir uma resposta clara ao usuário.

O ponto central deste projeto é demonstrar que a IA não executa diretamente as regras de negócio: ela identifica a intenção e solicita uma função; o backend continua responsável por autorização, validação e execução.

Exemplo de uso

Usuário: Qual é o saldo do cliente 101?

Função solicitada: consultar_saldo_cliente
Argumentos recebidos: {"clienteId":101}
Resultado do backend: {"Sucesso":true,"ClienteId":101,"Saldo":1250.50,...}

IA: O cliente 101 possui saldo de R$ 1.250,50.

O que é Function Calling?

Function Calling permite disponibilizar funções de uma aplicação para um modelo de IA por meio de descrições e schemas JSON.

O modelo não acessa diretamente banco de dados, serviços internos ou métodos C#. Em vez disso, ele pode devolver uma solicitação estruturada contendo:

o nome da função que deseja utilizar;

os argumentos necessários para executá-la;

um identificador para relacionar a execução com a conversa.

A aplicação recebe essa solicitação, verifica se a função é permitida, valida os argumentos e decide se deve executar a operação.

Arquitetura do fluxo

sequenceDiagram
    participant U as Usuário
    participant A as Aplicação .NET
    participant O as OpenAI
    participant B as Backend

    U->>A: Pergunta em linguagem natural
    A->>O: Pergunta + definição da ferramenta
    O-->>A: function_call + clienteId
    A->>A: Autoriza e valida argumentos
    A->>B: ConsultarSaldoCliente
    B-->>A: Resultado estruturado
    A->>O: function_call_output
    O-->>A: Resposta final
    A-->>U: Texto em linguagem natural

Tecnologias utilizadas

Tecnologia

Finalidade

.NET 9

Plataforma da aplicação Console

C#

Implementação do fluxo e regras de validação

HttpClient

Comunicação direta com a API da OpenAI

Responses API

Orquestração da conversa e da chamada de função

JSON Schema

Contrato dos argumentos enviados pelo modelo

System.Text.Json

Serialização, leitura e validação das respostas

Variável de ambiente

Proteção da chave da API

O projeto não depende de um SDK adicional da OpenAI. A integração é feita diretamente pelo endpoint HTTP https://api.openai.com/v1/responses.

Funcionalidades demonstradas

Leitura da chave por meio de OPENAI_API_KEY;

envio de instruções e pergunta para a Responses API;

declaração da função consultar_saldo_cliente;

schema estrito com additionalProperties: false;

desativação de chamadas paralelas neste exemplo;

identificação de uma resposta do tipo function_call;

lista explícita de funções autorizadas;

desserialização segura dos argumentos;

validação do código do cliente entre 1 e 9999;

execução da regra no backend;

envio do resultado como function_call_output;

continuidade da conversa com previous_response_id;

tratamento de resposta direta quando nenhuma função é solicitada.

Pré-requisitos

.NET SDK 9;

chave válida da API da OpenAI;

acesso à internet;

modelo configurado disponível para a sua conta da API.

Confira a versão do .NET:

dotnet --version

Como executar

1. Clone o repositório

git clone https://github.com/jeanalgoritimo/FunctionCallingSimples.git
cd FunctionCallingSimples

O branch padrão atual é master.

2. Configure a chave da OpenAI

PowerShell

$env:OPENAI_API_KEY="sua-chave-aqui"

Prompt de Comando

set OPENAI_API_KEY=sua-chave-aqui

Linux ou macOS

export OPENAI_API_KEY="sua-chave-aqui"

Nunca grave uma chave real no código, em arquivos versionados ou no README. Caso uma chave seja publicada, revogue-a imediatamente.

3. Execute a aplicação

dotnet restore
dotnet run

Digite uma pergunta, por exemplo:

Qual é o saldo do cliente 101?

Clientes disponíveis na simulação

Cliente

Saldo simulado

101

R$ 1.250,50

102

-R$ 320,75

103

R$ 5.000,00

Outros códigos válidos retornam a mensagem Cliente não encontrado. Valores menores que 1 ou maiores que 9999 são rejeitados antes da execução da função.

Entendendo o código

1. Configuração segura

A chave é obtida da variável de ambiente:

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

Se ela não estiver configurada, a aplicação é encerrada sem enviar requisições.

2. Definição da ferramenta

A ferramenta informa ao modelo que existe uma operação chamada consultar_saldo_cliente e que ela exige um clienteId inteiro.

var ferramenta = new
{
    type = "function",
    name = "consultar_saldo_cliente",
    description = "Consulta o saldo financeiro de um cliente usando seu código numérico.",
    parameters = new
    {
        type = "object",
        properties = new
        {
            clienteId = new { type = "integer" }
        },
        required = new[] { "clienteId" },
        additionalProperties = false
    },
    strict = true
};

O modo estrito ajuda o modelo a respeitar o contrato, mas não substitui a validação no backend.

3. Primeira chamada

A aplicação envia a pergunta, as instruções e a ferramenta. O modelo pode:

responder diretamente, quando não precisa da função; ou

retornar um item function_call com o nome e os argumentos.

4. Autorização e validação

Antes de executar qualquer método, a aplicação confirma o nome:

if (nomeFuncao != "consultar_saldo_cliente")
{
    Console.WriteLine("A IA solicitou uma função não autorizada.");
    return;
}

Depois, desserializa os argumentos e verifica o intervalo permitido. Essa etapa impede que a resposta do modelo seja tratada como uma ordem confiável.

5. Execução no backend

ConsultarSaldoCliente representa um serviço real da aplicação. Neste exemplo, os valores ficam em um Dictionary<int, decimal>, mas em produção essa função poderia chamar uma API, um banco de dados ou uma camada de domínio.

6. Retorno da ferramenta

O resultado é serializado e enviado para a API como function_call_output. O call_id relaciona o resultado à solicitação original e previous_response_id mantém a continuidade do fluxo.

Somente depois disso o modelo produz a resposta final para o usuário.

Estrutura do projeto

FunctionCallingSimples/
├── Program.cs                       # Fluxo completo de Function Calling
├── FunctionCallingSimples.csproj    # Projeto Console em .NET 9
├── FunctionCallingSimples.sln       # Solução do Visual Studio
└── .github/
    └── copilot-instructions.md

Controles de segurança presentes

Controle

Benefício

Chave em variável de ambiente

Evita credencial escrita no código

Função permitida explicitamente

Impede execução arbitrária de métodos

Schema estrito

Restringe formato e propriedades dos argumentos

Desserialização tipada

Converte a entrada para um contrato conhecido

Validação de intervalo

Aplica regra de negócio antes da execução

parallel_tool_calls = false

Limita o exemplo a uma função por vez

Backend como executor

Mantém dados e decisões fora do controle do modelo

Limitações atuais

Existe somente uma ferramenta;

os saldos são dados fixos em memória;

não há autenticação do usuário nem autorização por cliente;

não há confirmação humana para operações sensíveis;

não há retry, timeout explícito ou tratamento de rate limit;

não há logs estruturados ou trilha de auditoria;

o JSON bruto de erro pode ser exibido no Console;

não há testes automatizados;

o programa realiza uma pergunta e encerra;

o modelo está definido diretamente no código.

Próximas evoluções

Extrair a integração com OpenAI para um serviço;

usar IHttpClientFactory em uma aplicação ASP.NET Core;

mover modelo, endpoint e limites para configuração;

adicionar múltiplas funções autorizadas;

integrar banco de dados ou API real;

implementar autenticação e autorização por usuário;

exigir confirmação para ações que alterem dados;

adicionar timeout, retry com backoff e tratamento de HTTP 429;

registrar chamada, argumentos validados, usuário e resultado em auditoria;

incluir testes unitários e de integração;

criar uma interface web ou chat corporativo.

Boas práticas para aplicações reais

Considere todos os argumentos produzidos pelo modelo como entrada não confiável;

mantenha uma lista fechada de funções permitidas;

valide tipos, intervalos, formatos e regras de negócio no servidor;

verifique a identidade e as permissões do usuário antes da execução;

não permita que o modelo envie SQL, scripts ou URLs arbitrárias para execução;

solicite confirmação humana para pagamentos, exclusões e alterações críticas;

limite quantidade de chamadas, tempo, custo e volume de dados;

remova informações sensíveis de prompts, respostas e logs;

mantenha segredos em um cofre seguro, como Azure Key Vault ou Secret Manager;

registre auditoria suficiente para explicar quem solicitou e o que foi executado.

Solução de problemas

A variável OPENAI_API_KEY não foi configurada

Configure a variável no mesmo terminal em que executará dotnet run. Ao abrir outro terminal, talvez seja necessário configurá-la novamente.

Erro HTTP 401

Verifique se a chave está correta, ativa e sem espaços extras.

Erro HTTP 429

Confira saldo, cota e limites de requisição. Aguarde antes de repetir a chamada e implemente retry com backoff para cenários de produção.

Modelo indisponível

O exemplo utiliza o modelo declarado em Program.cs. Caso ele não esteja disponível para a sua conta, escolha um modelo compatível com a Responses API e suporte a ferramentas.

Cliente não encontrado

Para obter um saldo existente na demonstração, utilize os códigos 101, 102 ou 103.

Autor

Desenvolvido por Jean Paiva da Silva.

GitHub: @jeanalgoritimo

LinkedIn: Jean Silva

Repositório: jeanalgoritimo/FunctionCallingSimples
