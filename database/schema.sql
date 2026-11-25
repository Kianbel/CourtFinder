-- CourtFinder: Supabase schema (v2, replaces the old one)
-- Run once in the Supabase SQL Editor on a fresh project.
-- Design notes:
--   * Any signed-in user can list a court (owner = whoever created it). No roles to manage.
--   * Price lives on court_sports, so a venue can charge differently per sport.
--   * court_cards is a read-only view the app uses for the list + map (one query, no joins in C#).

-- ============================================================
-- Helpers
-- ============================================================
create function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.updated_at = now();
  return new;
end;
$$;

-- ============================================================
-- Profiles (one row per auth user, created automatically on sign-up)
-- ============================================================
create table public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null check (char_length(display_name) between 1 and 60),
  created_at timestamptz not null default now()
);

create function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(nullif(trim(new.raw_user_meta_data ->> 'display_name'), ''), split_part(new.email, '@', 1))
  );
  return new;
end;
$$;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- ============================================================
-- Lookups
-- ============================================================
create table public.sports (
  id smallint generated always as identity primary key,
  slug text not null unique,
  name text not null
);

insert into public.sports (slug, name) values
  ('pickleball', 'Pickleball'),
  ('badminton',  'Badminton'),
  ('basketball', 'Basketball'),
  ('volleyball', 'Volleyball'),
  ('tennis',     'Tennis'),
  ('futsal',     'Futsal');

create table public.amenities (
  id smallint generated always as identity primary key,
  slug text not null unique,
  name text not null
);

insert into public.amenities (slug, name) values
  ('lights',          'Lights'),
  ('parking',         'Parking'),
  ('restrooms',       'Restrooms'),
  ('lockers',         'Lockers'),
  ('showers',         'Showers'),
  ('air-conditioned', 'Air-conditioned'),
  ('covered',         'Covered'),
  ('equipment',       'Equipment rental'),
  ('water-refill',    'Water refill'),
  ('snack-bar',       'Snack bar'),
  ('bleachers',       'Bleachers'),
  ('coaching',        'Coaching');

-- ============================================================
-- Courts (a venue / facility)
-- ============================================================
create table public.courts (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null references public.profiles (id) on delete cascade,
  name text not null check (char_length(name) between 3 and 100),
  description text check (char_length(description) <= 2000),
  address text not null,
  city text not null,
  province text,
  latitude double precision not null check (latitude between -90 and 90),
  longitude double precision not null check (longitude between -180 and 180),
  is_indoor boolean not null default false,
  surface text,
  opening_time time,
  closing_time time,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create trigger courts_set_updated_at
  before update on public.courts
  for each row execute function public.set_updated_at();

create index courts_owner_id_idx on public.courts (owner_id);
create index courts_city_idx on public.courts (city);

-- Sports offered at a venue, with per-sport court count and hourly price
create table public.court_sports (
  court_id uuid not null references public.courts (id) on delete cascade,
  sport_id smallint not null references public.sports (id) on delete restrict,
  court_count smallint not null default 1 check (court_count > 0),
  price_per_hour numeric(10, 2) not null default 0 check (price_per_hour >= 0),
  primary key (court_id, sport_id)
);
create index court_sports_sport_id_idx on public.court_sports (sport_id);

create table public.court_amenities (
  court_id uuid not null references public.courts (id) on delete cascade,
  amenity_id smallint not null references public.amenities (id) on delete restrict,
  primary key (court_id, amenity_id)
);

-- Where to book. 'url' and 'messenger' hold links, 'phone' a number, 'walk_in' a short note.
create table public.booking_links (
  id uuid primary key default gen_random_uuid(),
  court_id uuid not null references public.courts (id) on delete cascade,
  kind text not null check (kind in ('url', 'messenger', 'phone', 'walk_in')),
  value text not null check (char_length(value) between 1 and 300),
  created_at timestamptz not null default now()
);
create index booking_links_court_id_idx on public.booking_links (court_id);

-- Photos live in the 'court-photos' storage bucket as '<court_id>/<filename>'
create table public.court_photos (
  id uuid primary key default gen_random_uuid(),
  court_id uuid not null references public.courts (id) on delete cascade,
  storage_path text not null,
  sort_order smallint not null default 0,
  created_at timestamptz not null default now()
);
create index court_photos_court_id_idx on public.court_photos (court_id);

-- ============================================================
-- Reviews (one per user per court; owners can't review their own venue)
-- ============================================================
create table public.reviews (
  id uuid primary key default gen_random_uuid(),
  court_id uuid not null references public.courts (id) on delete cascade,
  user_id uuid not null references public.profiles (id) on delete cascade,
  rating smallint not null check (rating between 1 and 5),
  comment text check (char_length(comment) <= 1000),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  unique (court_id, user_id)
);
create index reviews_court_id_idx on public.reviews (court_id);

create trigger reviews_set_updated_at
  before update on public.reviews
  for each row execute function public.set_updated_at();

-- ============================================================
-- court_cards: flat read model for the list, filters and map
-- ============================================================
create view public.court_cards
with (security_invoker = true) as
select
  c.id,
  c.name,
  c.address,
  c.city,
  c.province,
  c.latitude,
  c.longitude,
  c.is_indoor,
  (select min(cs.price_per_hour) from public.court_sports cs where cs.court_id = c.id) as min_price,
  coalesce(
    (select array_agg(s.slug order by s.name)
       from public.court_sports cs join public.sports s on s.id = cs.sport_id
      where cs.court_id = c.id),
    '{}'::text[]
  ) as sports,
  (select round(avg(r.rating)::numeric, 1) from public.reviews r where r.court_id = c.id) as avg_rating,
  (select count(*)::int from public.reviews r where r.court_id = c.id) as review_count,
  (select p.storage_path from public.court_photos p
    where p.court_id = c.id order by p.sort_order, p.created_at limit 1) as cover_path
from public.courts c;

-- ============================================================
-- Row Level Security
-- ============================================================
alter table public.profiles        enable row level security;
alter table public.sports          enable row level security;
alter table public.amenities       enable row level security;
alter table public.courts          enable row level security;
alter table public.court_sports    enable row level security;
alter table public.court_amenities enable row level security;
alter table public.booking_links   enable row level security;
alter table public.court_photos    enable row level security;
alter table public.reviews         enable row level security;

-- Profiles: readable by all (reviews show names); users edit only their own display name
create policy "Profiles are public" on public.profiles
  for select using (true);
create policy "Users update own profile" on public.profiles
  for update to authenticated
  using (id = auth.uid()) with check (id = auth.uid());

-- Lookups: read-only
create policy "Sports are public" on public.sports for select using (true);
create policy "Amenities are public" on public.amenities for select using (true);

-- Courts: public read; any signed-in user can list a court; only its owner edits/deletes
create policy "Courts are public" on public.courts
  for select using (true);
create policy "Users create own courts" on public.courts
  for insert to authenticated
  with check (owner_id = auth.uid());
create policy "Owners update own courts" on public.courts
  for update to authenticated
  using (owner_id = auth.uid()) with check (owner_id = auth.uid());
create policy "Owners delete own courts" on public.courts
  for delete to authenticated
  using (owner_id = auth.uid());

-- Court detail tables: public read; the court's owner manages them
create policy "Court sports are public" on public.court_sports for select using (true);
create policy "Owners manage court sports" on public.court_sports
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Court amenities are public" on public.court_amenities for select using (true);
create policy "Owners manage court amenities" on public.court_amenities
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Booking links are public" on public.booking_links for select using (true);
create policy "Owners manage booking links" on public.booking_links
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Court photos are public" on public.court_photos for select using (true);
create policy "Owners manage court photos" on public.court_photos
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

-- Reviews: public read; signed-in users review other people's courts, once each
create policy "Reviews are public" on public.reviews for select using (true);
create policy "Users post own reviews" on public.reviews
  for insert to authenticated
  with check (
    user_id = auth.uid()
    and not exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid())
  );
create policy "Users edit own reviews" on public.reviews
  for update to authenticated
  using (user_id = auth.uid()) with check (user_id = auth.uid());
create policy "Users delete own reviews" on public.reviews
  for delete to authenticated
  using (user_id = auth.uid());

-- ============================================================
-- Storage: court photos (public bucket, 5 MB, images only)
-- ============================================================
insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('court-photos', 'court-photos', true, 5242880, array['image/jpeg', 'image/png', 'image/webp'])
on conflict (id) do nothing;

create policy "Owners upload court photos" on storage.objects
  for insert to authenticated
  with check (
    bucket_id = 'court-photos'
    and exists (
      select 1 from public.courts c
      where c.id::text = (storage.foldername(name))[1] and c.owner_id = auth.uid()
    )
  );

create policy "Owners update court photos" on storage.objects
  for update to authenticated
  using (
    bucket_id = 'court-photos'
    and exists (
      select 1 from public.courts c
      where c.id::text = (storage.foldername(name))[1] and c.owner_id = auth.uid()
    )
  );

create policy "Owners delete court photos" on storage.objects
  for delete to authenticated
  using (
    bucket_id = 'court-photos'
    and exists (
      select 1 from public.courts c
      where c.id::text = (storage.foldername(name))[1] and c.owner_id = auth.uid()
    )
  );