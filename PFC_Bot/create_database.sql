CREATE TYPE pfc_choice AS ENUM ('pierre', 'feuille', 'ciseaux', '');

CREATE TABLE users (
    id BIGINT GENERATED ALWAYS AS IDENTITY,
    pseudo VARCHAR(40),
    id_discord BIGINT,
    score BIGINT default 0,
    papoule int default 0
    );

CREATE TABLE fights (
    id_fight BIGINT GENERATED ALWAYS AS IDENTITY,
    id_attacker BIGINT,
    id_defender BIGINT,
    id_winner BIGINT,
    posting_date DATE NOT NULL DEFAULT CURRENT_DATE,
    ending_date DATE,
    choice_attacker CHAR(1) DEFAULT "",
    choice_defender CHAR(1) DEFAULT "",
    id_message_attacker BIGINT,
    id_message_defender BIGINT,
    cancel boolean DEFAULT FALSE,
    FOREIGN KEY(id_attacker) REFERENCES users(id),
    FOREIGN KEY(id_defender) REFERENCES users(id),
    FOREIGN KEY(id_winner) REFERENCES users(id)
    );

CREATE TABLE guilds (
    id_guild BIGINT PRIMARY KEY NOT NULL,
    id_guild_discord BIGINT,
    id_role_pillow_knight BIGINT,
    id_role_plebe BIGINT
    );

ALTER TABLE users
ALTER COLUMN signature_sentence
SET DEFAULT '';

ALTER TABLE users
ALTER COLUMN signature_url
SET DEFAULT '';

UPDATE users SET signature_sentence = '';
UPDATE users SET signature_url = '';

ALTER TABLE users ADD COLUMN win_in_a_row INT DEFAULT 0;
ALTER TABLE users ADD COLUMN max_win_in_a_row INT DEFAULT 0;

ALTER TABLE users ADD COLUMN defeat_in_a_row INT DEFAULT 0;
ALTER TABLE users ADD COLUMN max_defeat_in_a_row INT DEFAULT 0;