#language: pt-BR
Funcionalidade: Consulta da trilha de auditoria
  Como administrador
  Quero rastrear alterações da aplicação
  Para investigar quem alterou cada registro

  Cenário: Administrador consulta a auditoria paginada
    Dado que estou autenticado como "Administrator"
    Quando consulto os registros de auditoria
    Então a resposta deve ter status 200
    E a auditoria deve retornar uma coleção paginada

  Cenário: Organizador não pode consultar auditoria
    Dado que estou autenticado como "Organizer"
    Quando consulto os registros de auditoria
    Então a resposta deve ter status 403

  Cenário: Participante não pode consultar auditoria
    Dado que estou autenticado como "Participant"
    Quando consulto os registros de auditoria
    Então a resposta deve ter status 403
