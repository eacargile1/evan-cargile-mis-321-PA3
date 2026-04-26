-- CS2 Tactical Assistant — MySQL schema (JawsDB / Heroku compatible, utf8mb4)
-- Run after creating an empty database: mysql -u ... -p cs2_tactical < schema.sql

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

DROP TABLE IF EXISTS chat_logs;
DROP TABLE IF EXISTS saved_strats;
DROP TABLE IF EXISTS reminders;
DROP TABLE IF EXISTS knowledge_chunks;
DROP TABLE IF EXISTS lineup_library;
DROP TABLE IF EXISTS pro_matches;
DROP TABLE IF EXISTS users;

SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE users (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  username VARCHAR(64) NOT NULL,
  email VARCHAR(120) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_users_username (username)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE knowledge_chunks (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  category VARCHAR(48) NOT NULL,
  title VARCHAR(255) NOT NULL,
  content MEDIUMTEXT NOT NULL,
  tags VARCHAR(512) NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  FULLTEXT KEY ft_knowledge_title_content (title, content),
  KEY ix_knowledge_category (category)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE chat_logs (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NULL,
  role VARCHAR(16) NOT NULL,
  content MEDIUMTEXT NOT NULL,
  meta JSON NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY ix_chat_user (user_id),
  CONSTRAINT fk_chat_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE saved_strats (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NOT NULL,
  title VARCHAR(200) NOT NULL,
  body JSON NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY ix_strats_user (user_id),
  CONSTRAINT fk_strats_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE reminders (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id BIGINT UNSIGNED NOT NULL,
  remind_at DATETIME NOT NULL,
  message VARCHAR(500) NOT NULL,
  created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY ix_reminders_user_time (user_id, remind_at),
  CONSTRAINT fk_reminders_user FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE pro_matches (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  tournament_name VARCHAR(160) NOT NULL,
  team_a VARCHAR(80) NOT NULL,
  team_b VARCHAR(80) NOT NULL,
  match_time_utc DATETIME NOT NULL,
  status VARCHAR(40) NOT NULL DEFAULT 'scheduled',
  notes TEXT NULL,
  PRIMARY KEY (id),
  KEY ix_matches_time (match_time_utc),
  KEY ix_matches_tournament (tournament_name)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE lineup_library (
  id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  map_name VARCHAR(40) NOT NULL,
  site VARCHAR(24) NOT NULL,
  grenade_type VARCHAR(16) NOT NULL,
  side CHAR(2) NOT NULL,
  lineup_name VARCHAR(160) NOT NULL,
  purpose VARCHAR(255) NOT NULL,
  instructions TEXT NOT NULL,
  when_to_use VARCHAR(255) NOT NULL,
  PRIMARY KEY (id),
  KEY ix_lineup_lookup (map_name, site, grenade_type, side)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
