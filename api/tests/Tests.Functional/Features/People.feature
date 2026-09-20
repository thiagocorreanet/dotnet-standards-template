#language: pt-BR
Funcionalidade: Gestão de pessoas
  Como administrador
  Quero cadastrar palestrantes e participantes
  Para vinculá-los aos eventos e palestras

  Contexto:
    Dado que estou autenticado como "Administrator"

  Cenário: Cadastrar e consultar uma pessoa
    Quando cadastro uma pessoa chamada "Ada Lovelace"
    Então a resposta deve ter status 201
    E consigo consultar a pessoa cadastrada

  Cenário: E-mail de pessoa deve ser único
    Dado que cadastrei uma pessoa chamada "Grace Hopper"
    Quando tento cadastrar outra pessoa com o mesmo e-mail
    Então a resposta deve ter status 409
    E a resposta deve ser um problema com código "People.EmailAlreadyRegistered"

  Cenário: Exclusão de pessoa é lógica e remove o registro das consultas
    Dado que cadastrei uma pessoa chamada "Margaret Hamilton"
    Quando excluo a pessoa cadastrada
    Então a resposta deve ter status 204
    E a pessoa cadastrada não deve mais ser encontrada

  Cenário: E-mail inválido é rejeitado
    Quando tento cadastrar uma pessoa com e-mail inválido
    Então a resposta deve ter status 400
    E a resposta deve ser um problema com código "Validation"

  Cenário: Administrador pode atualizar os dados da pessoa
    Dado que cadastrei uma pessoa chamada "Katherine Johnson"
    Quando altero o nome da pessoa para "Katherine Coleman Johnson"
    Então a resposta deve ter status 200
    E consigo consultar a pessoa com o nome "Katherine Coleman Johnson"

  Cenário: Participante pode cadastrar seu próprio perfil
    Dado que estou autenticado como "Participant"
    Quando cadastro uma pessoa chamada "Meu perfil"
    Então a resposta deve ter status 201
