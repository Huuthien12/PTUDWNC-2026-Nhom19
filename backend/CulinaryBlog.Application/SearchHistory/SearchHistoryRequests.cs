using CulinaryBlog.Application.Common.Interfaces;
using SearchHistoryEntity = CulinaryBlog.Domain.Entities.SearchHistory;
using FluentValidation;
using MediatR;

namespace CulinaryBlog.Application.SearchHistory;

public sealed record SearchHistoryDto(Guid Id, string Query, DateTime SearchedAt);
public sealed record CreateSearchHistoryRequest(string Query);
public sealed record GetSearchHistoryQuery(string UserId) : IRequest<IReadOnlyList<SearchHistoryDto>>;
public sealed record CreateSearchHistoryCommand(CreateSearchHistoryRequest Request, string UserId) : IRequest<SearchHistoryDto>;
public sealed record DeleteSearchHistoryCommand(Guid Id, string UserId) : IRequest;
public sealed record ClearSearchHistoryCommand(string UserId) : IRequest;

public sealed class CreateSearchHistoryCommandValidator : AbstractValidator<CreateSearchHistoryCommand>
{
    public CreateSearchHistoryCommandValidator()
    {
        RuleFor(x => x.Request.Query).Must(query => !string.IsNullOrWhiteSpace(query) && query.Trim().Length is >= 2 and <= 100).WithMessage("Query must contain between 2 and 100 characters.");
        RuleFor(x => x.Request.Query).Must(query => query?.Contains("password", StringComparison.OrdinalIgnoreCase) != true && query?.Contains("access_token", StringComparison.OrdinalIgnoreCase) != true && query?.Contains("bearer ", StringComparison.OrdinalIgnoreCase) != true).WithMessage("Sensitive values cannot be saved in search history.");
    }
}

public sealed class GetSearchHistoryQueryHandler(IUnitOfWork unitOfWork) : IRequestHandler<GetSearchHistoryQuery, IReadOnlyList<SearchHistoryDto>>
{
    public async Task<IReadOnlyList<SearchHistoryDto>> Handle(GetSearchHistoryQuery request, CancellationToken cancellationToken) => (await unitOfWork.SearchHistories.GetForUserAsync(request.UserId, cancellationToken)).Take(20).Select(x => new SearchHistoryDto(x.Id, x.Query, x.SearchedAt)).ToList();
}

public sealed class CreateSearchHistoryCommandHandler(IUnitOfWork unitOfWork, IValidator<CreateSearchHistoryCommand> validator, TimeProvider clock) : IRequestHandler<CreateSearchHistoryCommand, SearchHistoryDto>
{
    public async Task<SearchHistoryDto> Handle(CreateSearchHistoryCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var query = command.Request.Query.Trim().ToLowerInvariant();
        var now = clock.GetUtcNow().UtcDateTime;
        var item = await unitOfWork.SearchHistories.GetByQueryAsync(command.UserId, query, cancellationToken);
        if (item is null) { item = new SearchHistoryEntity { UserId = command.UserId, Query = query, SearchedAt = now }; await unitOfWork.SearchHistories.AddAsync(item, cancellationToken); }
        else item.SearchedAt = now;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var items = await unitOfWork.SearchHistories.GetForUserAsync(command.UserId, cancellationToken);
        unitOfWork.SearchHistories.RemoveRange(items.Skip(20));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SearchHistoryDto(item.Id, item.Query, item.SearchedAt);
    }
}

public sealed class DeleteSearchHistoryCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteSearchHistoryCommand>
{
    public async Task Handle(DeleteSearchHistoryCommand command, CancellationToken cancellationToken)
    {
        var item = await unitOfWork.SearchHistories.GetAsync(command.Id, command.UserId, cancellationToken) ?? throw new CulinaryBlog.Domain.Exceptions.NotFoundException("SEARCH_HISTORY_NOT_FOUND", "Search history item not found.");
        unitOfWork.SearchHistories.Remove(item); await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ClearSearchHistoryCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<ClearSearchHistoryCommand>
{
    public async Task Handle(ClearSearchHistoryCommand command, CancellationToken cancellationToken)
    {
        unitOfWork.SearchHistories.RemoveRange(await unitOfWork.SearchHistories.GetForUserAsync(command.UserId, cancellationToken)); await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
