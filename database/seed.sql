SET NAMES utf8mb4;

-- Idempotent re-seed for local grading / demos
SET FOREIGN_KEY_CHECKS = 0;
TRUNCATE TABLE chat_logs;
TRUNCATE TABLE saved_strats;
TRUNCATE TABLE reminders;
TRUNCATE TABLE knowledge_chunks;
TRUNCATE TABLE lineup_library;
TRUNCATE TABLE pro_matches;
TRUNCATE TABLE users;
SET FOREIGN_KEY_CHECKS = 1;

INSERT INTO users (id, username, email) VALUES
  (1, 'demo_coach', 'demo@example.com');

INSERT INTO knowledge_chunks (category, title, content, tags) VALUES
('economy', 'CS2 economy after losing pistol (T side)',
'After losing the pistol round as T, you start round 2 with $1900 base plus any kills/plants. Common paths: (1) Full eco — buy only pistols/smokes if you need a pick, stack money for round 3 rifle. (2) Force buy — buy MAC-10/MP9 + light utility if you believe you can punish a weak CT buy or get bomb plant money. (3) Half-buy — one player rifles if money pooled from plants, rest pistols. Loss bonus escalates: $1900 → $2400 → $2900 → $3400 max. Coordinate as a team: mixed buys lose to coordinated CT stacks.',
'pistol, loss bonus, force, eco'),

('economy', 'When to save instead of retake',
'Saving is correct when the retake win probability is low versus the value of weapons you would lose. Save if: time is short, you are isolated, utility is spent, or the bomb is planted with heavy CT post-plant crossfires. Force a retake attempt when you have numbers, kit, mollies to clear default plant spots, and time to trade. On partial saves, drop rifles for one anchor player who can afford next round full buy.',
'save, retake, economy'),

('maps', 'CT setups on Inferno B site',
'Default B: one player Banana deep with molly/smoke timing to delay T rushes; one Coffins or New Box to watch push from Banana; one rotates from mid/arch side to cut lurks. On execute calls, fall to site crossfire: CT spawn angle + coffins + optional boost on logs. Use molotovs on Banana choke and be careful of wallbangs from dark. Communicate whether you are "playing for info" or "committing early" to avoid double-peeking.',
'Inferno, B site, CT, setup'),

('maps', 'Mirage A site CT fundamentals',
'Standard A: one Connector/A ramp control, one under Palace/window for Palace pressure, one anchor Jungle/Stairs watching A main. On A executes, smoke CT, Jungle, Stairs as attackers — defenders pre-plan who flashes first contact and who plays time from Ticket/Firebox. Rotate from B only on confirmed bomb or clear sound cue to avoid fake rotations.',
'Mirage, A site, CT'),

('grenades', 'Mirage A site smoke fundamentals',
'Common A smokes: CT smoke from T spawn (blocks CT spawn vision), Jungle smoke (isolates connector plays), Stairs smoke (blocks stairs angle). Throw in coordinated sequence 20–25s before execute so they bloom together. Always pair with flash for ramp clear and molotov default plant behind triple.',
'Mirage, smoke, A site'),

('executes', 'Mirage B site full-buy execute (T)',
'Default mid presence to draw rotate, then hit B with window smoke, short smoke or market window block, and connector smoke if you fear flank. Entry order: short flash → short player → apps control → planter behind van. Post-plant: one market, one apps, one short/mid lurk delay.',
'Mirage, execute, B, full buy'),

('pistol_rounds', 'Ancient T-side pistol default',
'Ancient T pistol: contest mid or donut for map control early. Send two A main to pressure lamps, one mid timing peek, one B banana lurk timing. Goal is information + chip damage — first pick wins pistol more than raw aim. Buy kevlar on 3 players, utility on 2 if you have a set strat.',
'Ancient, pistol, T side'),

('roles', 'Five-man stack round roles',
'In structured teams: IGL calls pace; Entry creates space; Support throws set utility; Lurk denies rotates; AWPer holds long or picks mid. On executes, roles compress: two entries, one support, one lurk timing rotate, AWPer repositions for post-plant or mid deny.',
'roles, team, stack'),

('pro_play', 'Reading pro defaults vs executes',
'Pro teams layer defaults (mid control, shallow map pressure) before hitting set executes. Watch for utility count — four smokes thrown early often signals hit within 20 seconds. As a team, track whether opponents are "baiting utility" or committing based on sound and radar timing.',
'pro, meta'),

('defaults', 'T-side default pacing on any map',
'First 40 seconds: secure map control without overcommitting. Use one player to lurk opposite site to pin rotation. After mid control, choose execute based on numbers advantage or utility differential. If CTs over-rotate off sound, punish open site.',
'default, T side');

INSERT INTO lineup_library (map_name, site, grenade_type, side, lineup_name, purpose, instructions, when_to_use) VALUES
('Mirage', 'A', 'smoke', 'T', 'CT from T spawn', 'Blocks CT spawn vision for A execute', 'Align on T-side trash; aim at left antenna tip on building; jump-throw.', 'Full A execute or late map control hit'),
('Mirage', 'A', 'smoke', 'T', 'Jungle from ramp', 'Covers connector/jungle gap', 'Stand ramp corner; aim top of palace arch; left click throw.', 'When you need stairs player isolated'),
('Mirage', 'A', 'smoke', 'T', 'Stairs from ramp', 'Blocks stairs angle', 'Tuck ramp; aim under balcony lip; jump-throw.', 'Standard A site take'),
('Mirage', 'A', 'molly', 'T', 'Default plant behind triple', 'Forces planter off common plant', 'Palace edge; aim down at triple corner; run throw.', 'Post-plant or execute finish'),
('Inferno', 'B', 'smoke', 'T', 'CT smoke coffins', 'CT cross smoke for B', 'Second oranges; aim chimney; jump-throw.', 'Banana B execute'),
('Ancient', 'A', 'smoke', 'T', 'A main to elbow', 'Elbow control smoke', 'T spawn pillar; aim temple notch; standing throw.', 'Pistol or rifle A default');

-- pro_matches: table kept for schema / optional future use; live schedule uses HLTV (see hltv-bridge/).

INSERT INTO reminders (user_id, remind_at, message) VALUES
  (1, DATE_ADD(UTC_TIMESTAMP(), INTERVAL 1 DAY), 'Review Mirage B execute VOD');
