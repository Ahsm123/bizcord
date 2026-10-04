alter table members
    add constraint members_workspace_fk
    foreign key (workspace_id) references workspaces(id) on delete cascade;

alter table channels
    add constraint channels_workspace_fk foreign key (workspace_id) references workspaces (id) on delete cascade,                                                                                                                         
      add constraint channels_name_unique unique (workspace_id, name);

create index channels_workspaces_idx on channels (workspace_id);
    