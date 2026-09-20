#language: pt-BR
Funcionalidade: Identidade federada
  A API valida access tokens do provedor e mantém vínculos locais sem armazenar senhas.

  Cenário: Administrador provisiona vínculo externo
    Dado que estou autenticado como "Administrator"
    Quando provisiono uma identidade externa
    Então a resposta deve ter status 201
    E a resposta não deve conter access token

  Cenário: Participante não pode provisionar vínculos
    Dado que estou autenticado como "Participant"
    Quando provisiono uma identidade externa
    Então a resposta deve ter status 403

  Cenário: Subject duplicado não cria outro vínculo
    Dado que estou autenticado como "Administrator"
    E que provisionei uma identidade externa
    Quando provisiono uma identidade externa
    Então a resposta deve ter status 409
    E a resposta deve ser um problema com código "Identity.AlreadyLinked"

  Cenário: Perfil próprio é obtido com identidade federada
    Dado que estou autenticado como "Participant"
    Quando consulto minha identidade
    Então a resposta deve ter status 200
