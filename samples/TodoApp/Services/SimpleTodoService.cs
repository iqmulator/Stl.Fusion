﻿using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion;
using Stl.Fusion.Authentication;
using Stl.Fusion.Extensions;
using Templates.TodoApp.Abstractions;

namespace Templates.TodoApp.Services
{
    public class SimpleTodoService : ITodoService
    {
        private ImmutableList<Todo> _store = ImmutableList<Todo>.Empty; // It's always sorted by Id though

        // Commands

#pragma warning disable 1998
        public virtual async Task<Todo> AddOrUpdate(AddOrUpdateTodoCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return null!;

            var (session, todo) = command;
            if (string.IsNullOrEmpty(todo.Id))
                todo = todo with { Id = Ulid.NewUlid().ToString() };
            _store = _store.RemoveAll(i => i.Id == todo.Id).Add(todo);

            using var invalidating = Computed.Invalidate();
            _ = TryGet(session, todo.Id, CancellationToken.None);
            _ = PseudoGetAllItems(session);
            return todo;
        }

        public virtual async Task Remove(RemoveTodoCommand command, CancellationToken cancellationToken = default)
        {
            if (Computed.IsInvalidating()) return;

            var (session, todoId) = command;
            _store = _store.RemoveAll(i => i.Id == todoId);

            using var invalidating = Computed.Invalidate();
            _ = TryGet(session, todoId, CancellationToken.None);
            _ = PseudoGetAllItems(session);
        }
#pragma warning restore 1998

        // Queries

        public virtual Task<Todo?> TryGet(Session session, string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.SingleOrDefault(i => i.Id == id));

        public virtual async Task<Todo[]> List(Session session, PageRef<string> pageRef, CancellationToken cancellationToken = default)
        {
            await PseudoGetAllItems(session);
            return _store.OrderByAndTakePage(i => i.Id, pageRef).ToArray();
        }

        public virtual async Task<TodoSummary> GetSummary(Session session, CancellationToken cancellationToken = default)
        {
            await PseudoGetAllItems(session);
            var count = _store.Count();
            var doneCount = _store.Count(i => i.IsDone);
            return new TodoSummary(count, doneCount);
        }

        // Pseudo queries

        [ComputeMethod]
        protected virtual Task<Unit> PseudoGetAllItems(Session session)
            => TaskExt.UnitTask;
    }
}
