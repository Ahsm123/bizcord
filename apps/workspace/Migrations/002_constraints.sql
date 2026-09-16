alter table members
    add constraint members_workspace_fk
    foreign key (workspace_id) references workspaces(id) on delete cascade;
    
alter table channels
    add constraint channels_workspace_fk
    foreign key (workspace_id) references workspaces(id) on delete cascade;

create index channels_workspaces_idx on channels (workspace_id);
    