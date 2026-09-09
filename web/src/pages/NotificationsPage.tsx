import { useEffect, useState } from 'react';
import { Link, Navigate } from 'react-router-dom';

import { humanMessage } from '../api/client';
import { opportunityApi } from '../api/opportunity-client';
import type { HumanTask } from '../api/schemas';
import { useWorkspace } from '../auth/workspace-state';
import { LoadingState, MessageState } from '../components/PageState';
import { formatDateTime, humanizeCode } from '../presentation/format';
import { taskTarget } from '../tasks/task-target';

export function NotificationsPage() {
  const { selected, loading } = useWorkspace();
  const [tasks, setTasks] = useState<HumanTask[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!selected) return;
    let active = true;
    void opportunityApi.listTasks(selected.tenantId)
      .then((items) => { if (active) setTasks(items); })
      .catch((failure: unknown) => { if (active) setError(humanMessage(failure)); });
    return () => { active = false; };
  }, [selected]);

  if (loading) return <LoadingState />;
  if (!selected) return <Navigate to="/workspaces" replace />;
  if (error) return <MessageState title="Notifications could not be loaded" message={error} />;
  if (!tasks) return <LoadingState label="Loading notifications" />;

  return <section className="operations-page" aria-labelledby="notifications-title">
    <header className="operations-command-header"><div><p className="eyebrow">Attention alerts</p>
      <h1 id="notifications-title">Notifications</h1>
      <p>Notifications tell you what changed or needs attention. The underlying action remains owned by the assigned task.</p>
    </div><Link className="secondary-button" to="/tasks">Open assigned tasks</Link></header>
    <section className="operations-panel" aria-labelledby="notification-list-title">
      <header className="operations-panel-header"><div><p className="eyebrow">Current</p>
        <h2 id="notification-list-title">Items needing your attention</h2></div><span>{tasks.length}</span></header>
      {tasks.length === 0 ? <div className="operations-empty-row"><strong>You are up to date</strong>
        <p>No current decisions or exceptions are assigned to you.</p></div> :
        <div className="operations-table-scroll"><table className="operations-table">
          <thead><tr><th>Notification</th><th>Type</th><th>Assigned</th><th><span className="sr-only">Open</span></th></tr></thead>
          <tbody>{tasks.map((task) => <tr key={task.id}>
            <td><Link to={taskTarget(task)}><strong>{task.title}</strong></Link><small>{task.whyItMatters}</small></td>
            <td>{humanizeCode(task.taskType, true)}<small>{humanizeCode(task.resourceType, true)}</small></td>
            <td>{formatDateTime(task.createdAtUtc)}</td>
            <td><Link className="operations-row-action" to={taskTarget(task)} aria-label={`Open ${task.title}`}>→</Link></td>
          </tr>)}</tbody>
        </table></div>}
    </section>
  </section>;
}
