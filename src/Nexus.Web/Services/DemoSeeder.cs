using Microsoft.EntityFrameworkCore;
using Nexus.Domain.Entities;
using Nexus.Infrastructure.Data;

namespace Nexus.Web.Services;

// Dados de exemplo da demo do TCC no MonsterASP (branch demo/monsterasp-tcc). Só roda com
// DEMO_SEED=true e não faz nada se o workspace de demo já existir.
public static class DemoSeeder
{
    private const string Slug = "aamora-studio-demo";

    public static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        if (await db.Workspaces.AnyAsync(w => w.Slug == Slug))
        {
            await SeedConversationsAsync(services, logger);
            return;
        }

        var users = await db.Users
            .Where(u => u.NormalizedEmail!.EndsWith("@DEMO.AAMORA.COM.BR"))
            .ToDictionaryAsync(u => u.Email!.Split('@')[0], u => u);
        if (!users.ContainsKey("ana") || !users.ContainsKey("bruno") || !users.ContainsKey("carla"))
        {
            logger.LogWarning("Seed da demo: contas ana/bruno/carla @demo.aamora.com.br não encontradas.");
            return;
        }

        var ana = users["ana"].Id;
        var bruno = users["bruno"].Id;
        var carla = users["carla"].Id;
        var now = DateTime.UtcNow;

        var workspace = new Workspace
        {
            Id = Guid.NewGuid(),
            Name = "Aamora Studio",
            Slug = Slug,
            Color = "#6366f1",
            CreatedAtUtc = now
        };
        foreach (var (userId, role) in new[] { (ana, WorkspaceRole.Owner), (bruno, WorkspaceRole.Admin), (carla, WorkspaceRole.Member) })
        {
            workspace.Members.Add(new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspace.Id,
                UserId = userId,
                Role = role,
                JoinedAtUtc = now
            });
        }
        workspace.Subscription = WorkspaceSubscription.StartStarter(workspace.Id, now);
        db.Workspaces.Add(workspace);
        db.ChatConversations.Add(ChatConversationService.NewWorkspaceConversation(workspace.Id, now));

        var lists = new Dictionary<string, TaskList>();
        var spaceOrder = 0;
        foreach (var (spaceName, listNames) in new[]
        {
            ("App de Delivery", new[] { "Backlog", "Sprint atual" }),
            ("Site Institucional", new[] { "Conteúdo", "Desenvolvimento" }),
            ("Marketing", new[] { "Campanhas" }),
        })
        {
            var space = new Space { Id = Guid.NewGuid(), WorkspaceId = workspace.Id, Name = spaceName, SortOrder = spaceOrder++ };
            db.Spaces.Add(space);
            var listOrder = 0;
            foreach (var listName in listNames)
            {
                var list = new TaskList { Id = Guid.NewGuid(), SpaceId = space.Id, Name = listName, SortOrder = listOrder++ };
                list.Statuses.Add(new TaskStatusDefinition { Id = Guid.NewGuid(), TaskListId = list.Id, Name = "A Fazer", SortOrder = 0, Category = StatusCategory.ToDo, IsDefault = true });
                list.Statuses.Add(new TaskStatusDefinition { Id = Guid.NewGuid(), TaskListId = list.Id, Name = "Em Andamento", SortOrder = 1, Category = StatusCategory.InProgress });
                list.Statuses.Add(new TaskStatusDefinition { Id = Guid.NewGuid(), TaskListId = list.Id, Name = "Concluído", SortOrder = 2, Category = StatusCategory.Done });
                db.TaskLists.Add(list);
                lists[$"{spaceName}/{listName}"] = list;
            }
        }

        await db.SaveChangesAsync();

        // (lista, título, descrição, tipo, prioridade, status 0/1/2, responsável, prazo em dias)
        var tasks = new (string List, string Title, string Description, WorkItemType Type, WorkItemPriority Priority, int Status, string Assignee, int? DueInDays)[]
        {
            ("App de Delivery/Sprint atual", "Tela de login com e-mail e senha", "Login com validação de campos e mensagem de erro amigável.", WorkItemType.Story, WorkItemPriority.High, 2, ana, -3),
            ("App de Delivery/Sprint atual", "Carrinho de compras", "Adicionar, remover e alterar quantidade de itens; total atualizado na hora.", WorkItemType.Story, WorkItemPriority.Highest, 1, bruno, 2),
            ("App de Delivery/Sprint atual", "Integração com gateway de pagamento", "Pix e cartão de crédito no checkout.", WorkItemType.Task, WorkItemPriority.High, 1, ana, 5),
            ("App de Delivery/Sprint atual", "Pedido duplicado ao tocar duas vezes em Finalizar", "Desabilitar o botão enquanto a requisição está em andamento.", WorkItemType.Bug, WorkItemPriority.Highest, 0, bruno, 1),
            ("App de Delivery/Sprint atual", "Acompanhamento do pedido em tempo real", "Status: recebido, em preparo, saiu para entrega, entregue.", WorkItemType.Story, WorkItemPriority.Medium, 0, carla, 7),
            ("App de Delivery/Backlog", "Programa de fidelidade", "Pontos por pedido e cupom ao atingir a meta.", WorkItemType.Epic, WorkItemPriority.Medium, 0, ana, null),
            ("App de Delivery/Backlog", "Avaliação de restaurantes", "Nota de 1 a 5 e comentário após a entrega.", WorkItemType.Story, WorkItemPriority.Low, 0, carla, null),
            ("App de Delivery/Backlog", "Modo escuro", "Seguir a preferência do sistema operacional.", WorkItemType.Task, WorkItemPriority.Lowest, 0, bruno, null),
            ("Site Institucional/Conteúdo", "Texto da página Sobre nós", "História, missão e equipe.", WorkItemType.Task, WorkItemPriority.Medium, 2, carla, -5),
            ("Site Institucional/Conteúdo", "Fotos da equipe", "Sessão de fotos e tratamento das imagens.", WorkItemType.Task, WorkItemPriority.Low, 1, carla, 4),
            ("Site Institucional/Conteúdo", "Política de privacidade (LGPD)", "Revisar com o jurídico antes de publicar.", WorkItemType.Task, WorkItemPriority.High, 0, ana, 10),
            ("Site Institucional/Desenvolvimento", "Layout responsivo da home", "Mobile primeiro; testar em 360px.", WorkItemType.Story, WorkItemPriority.High, 1, bruno, 3),
            ("Site Institucional/Desenvolvimento", "Formulário de contato", "Envio por e-mail e proteção contra spam.", WorkItemType.Task, WorkItemPriority.Medium, 0, bruno, 8),
            ("Site Institucional/Desenvolvimento", "Menu não fecha no iPhone", "O menu lateral fica aberto depois de tocar num link.", WorkItemType.Bug, WorkItemPriority.High, 0, bruno, 2),
            ("Marketing/Campanhas", "Campanha de lançamento do app", "Posts, vídeo curto e e-mail para a base.", WorkItemType.Epic, WorkItemPriority.High, 1, ana, 14),
            ("Marketing/Campanhas", "Calendário de posts de dezembro", "Três posts por semana no Instagram.", WorkItemType.Task, WorkItemPriority.Medium, 0, carla, 6),
            ("Marketing/Campanhas", "Relatório de resultados de novembro", "Alcance, cliques e conversões.", WorkItemType.Task, WorkItemPriority.Low, 2, carla, -1),
        };

        var statusIds = await db.TaskStatusDefinitions
            .Where(s => lists.Values.Select(l => l.Id).Contains(s.TaskListId))
            .ToListAsync();

        using var scope = services.CreateScope();
        var workItems = scope.ServiceProvider.GetRequiredService<WorkItemService>();
        foreach (var t in tasks)
        {
            var list = lists[t.List];
            var status = statusIds.Where(s => s.TaskListId == list.Id).OrderBy(s => s.SortOrder).ElementAt(t.Status);
            var actorName = users.Values.First(u => u.Id == t.Assignee).DisplayName ;
            // O responsável é também o autor: assim o serviço não tenta mandar notificação por e-mail.
            var outcome = await workItems.CreateAsync(
                workspace.Id,
                new CreateWorkItemRequest(list.Id, t.Title, t.Description, t.Type, t.Priority, status.Id, t.Assignee,
                    t.DueInDays is null ? null : now.Date.AddDays(t.DueInDays.Value - 3),
                    t.DueInDays is null ? null : now.Date.AddDays(t.DueInDays.Value)),
                new WorkItemActor(t.Assignee, actorName));
            if (outcome.Status != WorkItemOutcomeStatus.Ok)
            {
                logger.LogWarning("Seed da demo: tarefa {Title} não criada ({Status}).", t.Title, outcome.Status);
            }
        }

        logger.LogWarning("Seed da demo: workspace Aamora Studio criado com {Count} tarefas.", tasks.Length);
        await SeedConversationsAsync(services, logger);
    }

    // Mensagens no chat do workspace, conversas individuais e comentários nas tarefas. Cada parte
    // só entra se ainda estiver vazia.
    private static async Task SeedConversationsAsync(IServiceProvider services, ILogger logger)
    {
        var dbFactory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();

        var workspace = await db.Workspaces.FirstAsync(w => w.Slug == Slug);
        var users = await db.Users
            .Where(u => u.NormalizedEmail!.EndsWith("@DEMO.AAMORA.COM.BR"))
            .ToDictionaryAsync(u => u.Email!.Split('@')[0], u => u.Id);
        var ana = users["ana"];
        var bruno = users["bruno"];
        var carla = users["carla"];
        var now = DateTime.UtcNow;

        using var scope = services.CreateScope();
        var chat = scope.ServiceProvider.GetRequiredService<ChatConversationService>();

        async Task AddMessagesAsync(Guid conversationId, (string User, string Text, int MinutesAgo)[] messages)
        {
            if (await db.ChatMessages.AnyAsync(m => m.ConversationId == conversationId))
            {
                return;
            }
            foreach (var m in messages)
            {
                db.ChatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspace.Id,
                    ConversationId = conversationId,
                    UserId = m.User,
                    Content = m.Text,
                    CreatedAtUtc = now.AddMinutes(-m.MinutesAgo)
                });
            }
        }

        var general = await chat.EnsureWorkspaceConversationAsync(workspace.Id);
        await AddMessagesAsync(general, new[]
        {
            (ana, "Bom dia, pessoal! Hoje começa a sprint nova do App de Delivery. 🚀", 300),
            (bruno, "Bom dia! Já peguei o carrinho de compras, deve ficar pronto até quinta.", 295),
            (carla, "Bom dia! Estou fechando os textos do site e depois parto para o calendário de posts.", 290),
            (ana, "Ótimo. Lembrem de mover as tarefas no quadro conforme forem andando, assim o painel fica certinho.", 285),
            (bruno, "Achei um bug: dá para finalizar o pedido duas vezes se tocar rápido no botão. Já abri a tarefa.", 120),
            (ana, "Boa, Bruno! Coloquei como prioridade máxima.", 115),
            (carla, "A sessão de fotos da equipe ficou para sexta às 14h, tudo bem para vocês?", 60),
            (bruno, "Por mim, ok.", 58),
            (ana, "Combinado! 📸", 55),
        });

        var anaBruno = await chat.GetOrCreateDirectAsync(workspace.Id, ana, bruno);
        if (anaBruno is not null)
        {
            await AddMessagesAsync(anaBruno.Value, new[]
            {
                (ana, "Bruno, como está a integração com o gateway de pagamento?", 240),
                (bruno, "O Pix já funciona no ambiente de testes. Falta o cartão de crédito.", 235),
                (ana, "Show. Consegue me mostrar amanhã na daily?", 232),
                (bruno, "Consigo sim, deixo preparado.", 230),
            });
        }

        var anaCarla = await chat.GetOrCreateDirectAsync(workspace.Id, ana, carla);
        if (anaCarla is not null)
        {
            await AddMessagesAsync(anaCarla.Value, new[]
            {
                (carla, "Ana, você pode revisar o texto da página Sobre nós?", 180),
                (ana, "Revisei agora, ficou ótimo! Só troquei uma palavra no segundo parágrafo.", 170),
                (carla, "Perfeito, obrigada! Já marquei como concluída.", 165),
            });
        }

        var brunoCarla = await chat.GetOrCreateDirectAsync(workspace.Id, bruno, carla);
        if (brunoCarla is not null)
        {
            await AddMessagesAsync(brunoCarla.Value, new[]
            {
                (carla, "Bruno, o menu do site não fecha no meu iPhone 😅", 90),
                (bruno, "Vi a tarefa. Acho que é o evento de toque, vou olhar ainda hoje.", 85),
            });
        }

        var comments = new (string Task, string User, string Text, int MinutesAgo)[]
        {
            ("Carrinho de compras", ana, "Lembrar de manter o carrinho salvo se o usuário fechar o app.", 280),
            ("Carrinho de compras", bruno, "Boa! Vou salvar localmente e sincronizar quando abrir de novo.", 270),
            ("Pedido duplicado ao tocar duas vezes em Finalizar", bruno, "Consegui reproduzir no Android e no iOS.", 118),
            ("Pedido duplicado ao tocar duas vezes em Finalizar", ana, "Precisamos corrigir antes do lançamento.", 110),
            ("Integração com gateway de pagamento", bruno, "Pix funcionando em homologação. Começando o cartão.", 230),
            ("Texto da página Sobre nós", ana, "Aprovado! ✅", 170),
            ("Layout responsivo da home", carla, "As fotos da equipe chegam na semana que vem, pode usar placeholder por enquanto.", 100),
            ("Layout responsivo da home", bruno, "Combinado, deixo o espaço reservado.", 95),
            ("Menu não fecha no iPhone", carla, "Acontece no Safari, no Chrome funciona normal.", 88),
            ("Campanha de lançamento do app", ana, "Vamos lançar junto com a versão 1.0, depois da correção do pedido duplicado.", 200),
            ("Campanha de lançamento do app", carla, "Já estou rascunhando o roteiro do vídeo.", 190),
        };

        var titles = comments.Select(c => c.Task).Distinct().ToList();
        var items = await db.WorkItems
            .Where(w => w.TaskList.Space.WorkspaceId == workspace.Id && titles.Contains(w.Title))
            .ToDictionaryAsync(w => w.Title, w => w.Id);
        var itemIds = items.Values.ToList();
        if (!await db.WorkItemComments.AnyAsync(c => itemIds.Contains(c.WorkItemId)))
        {
            foreach (var c in comments.Where(c => items.ContainsKey(c.Task)))
            {
                db.WorkItemComments.Add(new WorkItemComment
                {
                    Id = Guid.NewGuid(),
                    WorkItemId = items[c.Task],
                    UserId = c.User,
                    Content = c.Text,
                    CreatedAtUtc = now.AddMinutes(-c.MinutesAgo)
                });
            }
        }

        await db.SaveChangesAsync();
        logger.LogWarning("Seed da demo: mensagens e comentários conferidos.");
    }
}
