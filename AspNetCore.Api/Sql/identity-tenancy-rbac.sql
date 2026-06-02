create table tenants (
    id uniqueidentifier primary key,
    code nvarchar(50) not null unique,
    name nvarchar(100) not null,
    is_active bit not null,
    created_at datetime2 not null,
    updated_at datetime2 not null
);

create table users (
    id uniqueidentifier primary key,
    user_name nvarchar(50) not null unique,
    email nvarchar(100) not null unique,
    display_name nvarchar(100) not null,
    password_hash nvarchar(512) not null,
    password_salt nvarchar(256) not null,
    is_active bit not null,
    created_at datetime2 not null,
    updated_at datetime2 not null
);

create table roles (
    id uniqueidentifier primary key,
    tenant_id uniqueidentifier not null,
    code nvarchar(50) not null,
    name nvarchar(100) not null,
    description nvarchar(500) not null,
    is_default bit not null,
    created_at datetime2 not null,
    updated_at datetime2 not null,
    constraint fk_roles_tenants foreign key (tenant_id) references tenants(id) on delete cascade,
    constraint uq_roles_tenant_code unique (tenant_id, code),
    constraint uq_roles_tenant_name unique (tenant_id, name)
);

create table permissions (
    id uniqueidentifier primary key,
    code nvarchar(100) not null unique,
    name nvarchar(100) not null,
    type int not null, -- 1=Api, 2=Menu, 3=Button
    description nvarchar(500) not null,
    http_method nvarchar(20) not null,
    route nvarchar(200) not null,
    created_at datetime2 not null
);

create table menus (
    id uniqueidentifier primary key,
    parent_id uniqueidentifier null,
    code nvarchar(50) not null unique,
    name nvarchar(100) not null,
    path nvarchar(200) not null,
    component nvarchar(200) not null,
    icon nvarchar(100) not null,
    sort int not null,
    permission_code nvarchar(100) not null,
    created_at datetime2 not null,
    constraint fk_menus_parent foreign key (parent_id) references menus(id)
);

create table tenant_users (
    tenant_id uniqueidentifier not null,
    user_id uniqueidentifier not null,
    is_tenant_owner bit not null,
    joined_at datetime2 not null,
    constraint pk_tenant_users primary key (tenant_id, user_id),
    constraint fk_tenant_users_tenants foreign key (tenant_id) references tenants(id) on delete cascade,
    constraint fk_tenant_users_users foreign key (user_id) references users(id) on delete cascade
);

create table user_roles (
    tenant_id uniqueidentifier not null,
    user_id uniqueidentifier not null,
    role_id uniqueidentifier not null,
    assigned_at datetime2 not null,
    constraint pk_user_roles primary key (tenant_id, user_id, role_id),
    constraint fk_user_roles_users foreign key (user_id) references users(id) on delete cascade,
    constraint fk_user_roles_roles foreign key (role_id) references roles(id) on delete cascade
);

create table role_permissions (
    role_id uniqueidentifier not null,
    permission_id uniqueidentifier not null,
    granted_at datetime2 not null,
    constraint pk_role_permissions primary key (role_id, permission_id),
    constraint fk_role_permissions_roles foreign key (role_id) references roles(id) on delete cascade,
    constraint fk_role_permissions_permissions foreign key (permission_id) references permissions(id) on delete cascade
);
