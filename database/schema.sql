-- CourtFinder: Supabase schema
-- Run once in the Supabase SQL Editor on a fresh project.

-- ============================================================
-- Types
-- ============================================================
create type public.user_role as enum ('player', 'owner');
create type public.booking_kind as enum ('messenger', 'website', 'phone', 'walk_in');

-- ============================================================
-- Helper: keep updated_at current
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
-- Profiles (one row per auth user)
-- ============================================================
create table public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  display_name text not null check (char_length(display_name) between 1 and 60),
  role public.user_role not null default 'player',
  created_at timestamptz not null default now()
);

-- Create a profile automatically when someone signs up.
-- The app passes display_name and role in the sign-up metadata.
create function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
begin
  insert into public.profiles (id, display_name, role)
  values (
    new.id,
    coalesce(nullif(trim(new.raw_user_meta_data ->> 'display_name'), ''), split_part(new.email, '@', 1)),
    case when new.raw_user_meta_data ->> 'role' = 'owner'
         then 'owner'::public.user_role
         else 'player'::public.user_role end
  );
  return new;
end;
$$;

create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- ============================================================
-- Lookup tables: sports and amenities
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
-- Courts
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
  court_count smallint not null default 1 check (court_count > 0),
  surface text,
  price_per_hour numeric(10, 2) not null check (price_per_hour >= 0),
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

-- Which sports a court supports (a court can have several)
create table public.court_sports (
  court_id uuid not null references public.courts (id) on delete cascade,
  sport_id smallint not null references public.sports (id) on delete restrict,
  primary key (court_id, sport_id)
);
create index court_sports_sport_id_idx on public.court_sports (sport_id);

create table public.court_amenities (
  court_id uuid not null references public.courts (id) on delete cascade,
  amenity_id smallint not null references public.amenities (id) on delete restrict,
  primary key (court_id, amenity_id)
);

-- Where to book: link, number, or walk-in note
create table public.booking_links (
  id uuid primary key default gen_random_uuid(),
  court_id uuid not null references public.courts (id) on delete cascade,
  kind public.booking_kind not null,
  value text check (char_length(value) <= 300), -- URL, phone number, or walk-in note
  created_at timestamptz not null default now()
);
create index booking_links_court_id_idx on public.booking_links (court_id);

-- Photos: files live in the 'court-photos' storage bucket
create table public.court_photos (
  id uuid primary key default gen_random_uuid(),
  court_id uuid not null references public.courts (id) on delete cascade,
  storage_path text not null, -- e.g. '<court_id>/<filename>'
  sort_order smallint not null default 0,
  created_at timestamptz not null default now()
);
create index court_photos_court_id_idx on public.court_photos (court_id);

-- ============================================================
-- Reviews (one per user per court)
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

-- Average rating and review count per court
create view public.court_stats
with (security_invoker = true) as
select
  court_id,
  round(avg(rating)::numeric, 1) as avg_rating,
  count(*)::int as review_count
from public.reviews
group by court_id;

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

-- Profiles: public read (reviews show names); users edit only their own display name
create policy "Profiles are public" on public.profiles
  for select using (true);
create policy "Users update own profile" on public.profiles
  for update using (id = auth.uid()) with check (id = auth.uid());
revoke update on public.profiles from authenticated;
grant update (display_name) on public.profiles to authenticated;

-- Lookups: read-only for everyone
create policy "Sports are public" on public.sports
  for select using (true);
create policy "Amenities are public" on public.amenities
  for select using (true);

-- Courts: public read; only owner accounts create; only the owner edits or deletes
create policy "Courts are public" on public.courts
  for select using (true);
create policy "Owners create courts" on public.courts
  for insert to authenticated
  with check (
    owner_id = auth.uid()
    and exists (select 1 from public.profiles p where p.id = auth.uid() and p.role = 'owner')
  );
create policy "Owners update own courts" on public.courts
  for update to authenticated
  using (owner_id = auth.uid()) with check (owner_id = auth.uid());
create policy "Owners delete own courts" on public.courts
  for delete to authenticated
  using (owner_id = auth.uid());

-- Court detail tables: public read; the court's owner manages them
create policy "Court sports are public" on public.court_sports
  for select using (true);
create policy "Owners manage court sports" on public.court_sports
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Court amenities are public" on public.court_amenities
  for select using (true);
create policy "Owners manage court amenities" on public.court_amenities
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Booking links are public" on public.booking_links
  for select using (true);
create policy "Owners manage booking links" on public.booking_links
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

create policy "Court photos are public" on public.court_photos
  for select using (true);
create policy "Owners manage court photos" on public.court_photos
  for all to authenticated
  using (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()))
  with check (exists (select 1 from public.courts c where c.id = court_id and c.owner_id = auth.uid()));

-- Reviews: public read; signed-in users review other people's courts, once each
create policy "Reviews are public" on public.reviews
  for select using (true);
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
-- Storage: court photos
-- Files are stored as '<court_id>/<filename>'. Anyone can view; only
-- the court's owner can upload, replace or delete.
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