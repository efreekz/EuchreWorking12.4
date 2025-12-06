create or replace function is_lobby_ready(p_lobby_id text)
returns boolean
language plpgsql
as $$
begin
  return exists (
    select 1
    from lobbies
    where id = p_lobby_id
      and (created_at + (time_to_live * interval '1 second')) > now()
  );
end;
$$;