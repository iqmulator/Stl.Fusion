using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Stl.Fusion;
using Stl.Fusion.Authentication;
using Stl.Fusion.Extensions;
using Templates.TodoApp.Abstractions;

namespace Templates.TodoApp.Services
{
    public class TodoService : ITodoService
    {
        private readonly ISandboxedKeyValueStore _store;
        private readonly IAuthService _authService;

        public TodoService(ISandboxedKeyValueStore store, IAuthService authService)
        {
            _store = store;
            _authService = authService;
        }

        // Commands

        public virtual async Task<Todo> AddOrUpdate(AddOrUpdateTodoCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return default!;
            var (session, todo) = command;
            var user = await _authService.GetUser(session, cancellationToken);
            user.MustBeAuthenticated();

            Todo? oldTodo = null;
            if (string.IsNullOrEmpty(todo.Id))
                todo = todo with { Id = Ulid.NewUlid().ToString() };
            else
                oldTodo = await TryGet(session, todo.Id, cancellationToken);

            if (todo.Title.Contains("@"))
                throw new ValidationException("Todo title can't contain '@' symbol.");

            var key = GetTodoKey(user, todo.Id);
            await _store.Set(session, key, todo, cancellationToken);
            if (oldTodo?.IsDone != todo.IsDone) {
                var doneKey = GetDoneKey(user, todo.Id);
                if (todo.IsDone)
                    await _store.Set(session, doneKey, true, cancellationToken);
                else
                    await _store.Remove(session, doneKey, cancellationToken);
            }

            if (todo.Title.Contains("#"))
                throw new DbUpdateConcurrencyException(
                    "Simulated concurrency conflict. " +
                    "Check the log to see if OperationReprocessor retried the command 3 times.");

            return todo;
        }

        public virtual async Task Remove(RemoveTodoCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return;
            var (session, id) = command;
            var user = await _authService.GetUser(session, cancellationToken);
            user.MustBeAuthenticated();

            var key = GetTodoKey(user, id);
            var doneKey = GetDoneKey(user, id);
            await _store.Remove(session, key, cancellationToken);
            await _store.Remove(session, doneKey, cancellationToken);
        }

        // Queries

        public virtual async Task<Todo?> TryGet(Session session, string id, CancellationToken cancellationToken = default)
        {
            var user = await _authService.GetUser(session, cancellationToken);
            user.MustBeAuthenticated();

            var key = GetTodoKey(user, id);
            var todoOpt = await _store.TryGet<Todo>(session, key, cancellationToken);
            return todoOpt.IsSome(out var todo) ? todo : null;
        }

        public virtual async Task<Todo[]> List(Session session, PageRef<string> pageRef, CancellationToken cancellationToken = default)
        {
            var user = await _authService.GetUser(session, cancellationToken);
            user.MustBeAuthenticated();

            var keyPrefix = GetTodoKeyPrefix(user);
            var keySuffixes = await _store.ListKeySuffixes(session, keyPrefix, pageRef, cancellationToken);
            var tasks = keySuffixes.Select(suffix => _store.TryGet<Todo>(session, keyPrefix + suffix, cancellationToken));
            var todoOpts = await Task.WhenAll(tasks);
            return todoOpts.Where(todo => todo.HasValue).Select(todo => todo.Value).ToArray();
        }

        public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
        {
            var user = await _authService.GetUser(session, cancellationToken);
            user.MustBeAuthenticated();

            var count = await _store.Count(session, GetTodoKeyPrefix(user), cancellationToken);
            var doneCount = await _store.Count(session, GetDoneKeyPrefix(user), cancellationToken);
            return new TodoSummary(count, doneCount);
        }

        // Private methods

        private string GetTodoKey(User user, string id)
            => $"{GetTodoKeyPrefix(user)}/{id}";
        private string GetDoneKey(User user, string id)
            => $"{GetDoneKeyPrefix(user)}/{id}";

        private string GetTodoKeyPrefix(User user)
            => $"@user/{user.Id}/todo/items";
        private string GetDoneKeyPrefix(User user)
            => $"@user/{user.Id}/todo/done";
    }
}
