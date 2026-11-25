do $$
declare
  v_owner uuid := (select id from public.profiles order by created_at limit 1);
  v_court uuid;
begin
  if v_owner is null then
    raise exception 'Create a user first (Authentication > Users > Add user).';
  end if;

  -- 1. Outdoor pickleball
  insert into public.courts (owner_id, name, description, address, city, province, latitude, longitude, is_indoor, surface, opening_time, closing_time)
  values (v_owner, 'Demo Pickleball Hub', 'Four lit outdoor courts with a relaxed vibe. Great for beginners and weekend games.',
          'IT Park, Lahug', 'Cebu City', 'Cebu', 10.3280, 123.9060, false, 'Acrylic', '06:00', '22:00')
  returning id into v_court;
  insert into public.court_sports (court_id, sport_id, court_count, price_per_hour)
    select v_court, id, 4, 350 from public.sports where slug = 'pickleball';
  insert into public.court_amenities (court_id, amenity_id)
    select v_court, id from public.amenities where slug in ('lights','parking','restrooms','equipment','water-refill');
  insert into public.booking_links (court_id, kind, value) values
    (v_court, 'messenger', 'https://m.me/example'),
    (v_court, 'phone', '+63 900 000 0001');

  -- 2. Indoor multi-sport
  insert into public.courts (owner_id, name, description, address, city, province, latitude, longitude, is_indoor, surface, opening_time, closing_time)
  values (v_owner, 'Demo Indoor Sports Arena', 'Air-conditioned hall with badminton, volleyball and basketball setups.',
          'Banilad', 'Cebu City', 'Cebu', 10.3427, 123.9110, true, 'Wood / PVC', '08:00', '23:00')
  returning id into v_court;
  insert into public.court_sports (court_id, sport_id, court_count, price_per_hour)
    select v_court, id, 3, 300 from public.sports where slug = 'badminton';
  insert into public.court_sports (court_id, sport_id, court_count, price_per_hour)
    select v_court, id, 1, 900 from public.sports where slug in ('basketball','volleyball');
  insert into public.court_amenities (court_id, amenity_id)
    select v_court, id from public.amenities where slug in ('air-conditioned','lockers','showers','restrooms','bleachers','snack-bar');
  insert into public.booking_links (court_id, kind, value) values
    (v_court, 'url', 'https://example.com/book'),
    (v_court, 'phone', '+63 900 000 0002');

  -- 3. Covered basketball court
  insert into public.courts (owner_id, name, description, address, city, province, latitude, longitude, is_indoor, surface, opening_time, closing_time)
  values (v_owner, 'Demo Covered Court', 'Neighborhood covered court, walk-ins welcome.',
          'Mandaue City', 'Mandaue', 'Cebu', 10.3236, 123.9223, false, 'Concrete', '05:00', '21:00')
  returning id into v_court;
  insert into public.court_sports (court_id, sport_id, court_count, price_per_hour)
    select v_court, id, 1, 500 from public.sports where slug in ('basketball','volleyball');
  insert into public.court_amenities (court_id, amenity_id)
    select v_court, id from public.amenities where slug in ('covered','lights','restrooms','parking');
  insert into public.booking_links (court_id, kind, value) values
    (v_court, 'walk_in', 'First come, first served. Reserve ahead on weekends.');

  -- 4. Badminton club in Lapu-Lapu
  insert into public.courts (owner_id, name, description, address, city, province, latitude, longitude, is_indoor, surface, opening_time, closing_time)
  values (v_owner, 'Demo Badminton Club', 'Six-court badminton hall with coaching available.',
          'Mactan', 'Lapu-Lapu', 'Cebu', 10.3103, 123.9494, true, 'PVC', '07:00', '22:00')
  returning id into v_court;
  insert into public.court_sports (court_id, sport_id, court_count, price_per_hour)
    select v_court, id, 6, 250 from public.sports where slug = 'badminton';
  insert into public.court_amenities (court_id, amenity_id)
    select v_court, id from public.amenities where slug in ('air-conditioned','coaching','equipment','parking','restrooms');
  insert into public.booking_links (court_id, kind, value) values
    (v_court, 'messenger', 'https://m.me/example2');
end $$;