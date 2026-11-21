DO $$
BEGIN
  IF current_database() <> 'bevn101' THEN
    RAISE EXCEPTION 'Refusing to run seed.sql against database %; expected bevn101', current_database();
  END IF;
END $$;

-- Purpose: Seed deterministic conference and session fixtures for the legacy API benchmark.
-- Prerequisites: Connect to the disposable bevn101 database with the API schema already created.
-- The DO block above enforces the database name. Verify the server is disposable as well.
-- Usage: Run via smoke.py or, from this directory, use psql -v ON_ERROR_STOP=1 -d bevn101 -f seed.sql.
-- ON_ERROR_STOP ensures psql stops before the inserts if the guard raises an exception.
-- Safety: Inserts fixtures only. Deletes no rows. Use a fresh database and keep dev/prod data out of the run.

BEGIN;

-- Fixed IDs match smoke.py's request cases and keep the fixture responses reproducible.
INSERT INTO conferences ("Id", "Title", "StartDate", "EndDate", "Status")
SELECT 10100 + size, 'BEVN-101 conference ' || size,
       '2026-01-01 09:00:00', '2026-01-01 17:00:00', 'UPCOMING'
FROM (VALUES (0), (1), (10), (50)) AS fixtures(size);

INSERT INTO conferences ("Id", "Title", "StartDate", "EndDate", "Status")
VALUES (10199, 'BEVN-101 related conference', '2026-01-01 09:00:00', '2026-01-01 17:00:00', 'UPCOMING');

INSERT INTO speakers ("Id", "FirstName", "LastName")
SELECT 11000 + number, 'Speaker', number::text FROM generate_series(1, 61) AS number;

INSERT INTO rooms ("Id", "Name", "Capacity")
SELECT 12000 + number, 'Room ' || number, 50 FROM generate_series(1, 61) AS number;

-- Give the one-session case both references. Larger cases cycle through all
-- four nullability combinations. The ten-session case shares one speaker
-- and one room, while the fifty-session case uses distinct principals.
INSERT INTO sessions ("Id", "ConferenceId", "Title", "StartTime", "EndTime", "Capacity", "SpeakerId", "RoomId")
SELECT 10200 + size * 100 + number, 10100 + size, 'Session ' || number,
       '2026-01-01 10:00:00', '2026-01-01 11:00:00', 25,
       CASE WHEN size = 1 THEN 11061 WHEN size = 10 AND number = 5 THEN 11051
            WHEN number % 4 IN (1, 3) THEN 11000 + offset_id + number END,
       CASE WHEN size = 1 THEN 12061 WHEN size = 10 AND number = 6 THEN 12052
            WHEN number % 4 IN (2, 3) THEN 12000 + offset_id + number END
FROM (VALUES (1, 0), (10, 50), (50, 0)) AS fixtures(size, offset_id)
CROSS JOIN LATERAL generate_series(1, size) AS number;

-- This session is reachable from the ten-session response by the shared
-- speaker's inverse collection. It is outside the root conference filter.
INSERT INTO sessions ("Id", "ConferenceId", "Title", "StartTime", "EndTime", "Capacity", "SpeakerId")
VALUES (16000, 10199, 'Related session', '2026-01-01 10:00:00', '2026-01-01 11:00:00', 25, 11051);

COMMIT;
