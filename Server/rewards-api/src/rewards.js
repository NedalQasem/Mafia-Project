'use strict';
// Pure rules: what a finished match is worth. No network, no files, so it is easy to test and to change from config.json.

const ROLES = ['Mafia', 'Doctor', 'Sheriff', 'Civilian'];

/** Same curve as the game's PlayerProfile: the next level needs 100 + level * 50 XP. */
function xpForLevel(level) { return 100 + level * 50; }

function addXp(progress, amount) {
  progress.xp += amount;
  while (progress.xp >= progress.xpToNext) {
    progress.xp -= progress.xpToNext;
    progress.level += 1;
    progress.xpToNext = xpForLevel(progress.level);
  }
}

function newProgress() {
  return {
    level: 1, xp: 0, xpToNext: xpForLevel(1), rankPoints: 0,
    games: 0, wins: 0, losses: 0, mafiaGames: 0, mafiaWins: 0, citizenGames: 0, citizenWins: 0,
    lastReward: '',
  };
}

/** Team of a role: the mafia on one side, everybody else (town) on the other. */
function teamOf(role) { return role === 'Mafia' ? 'Mafia' : 'Town'; }

/**
 * Checks the shape of a report. Returns an error string or null.
 * players: PlayFab ids of everybody who played; winner: 'Town' | 'Mafia'.
 */
function validateReport(r, cfg) {
  if (!r || typeof r !== 'object') return 'bad body';
  if (typeof r.matchId !== 'string' || !/^[A-Za-z0-9_-]{8,64}$/.test(r.matchId)) return 'bad matchId';
  if (!Array.isArray(r.players)) return 'bad players';
  const n = r.players.length;
  if (n < cfg.minPlayers || n > cfg.maxPlayers) return 'player count out of range';
  const seen = new Set();
  for (const p of r.players) {
    if (typeof p !== 'string' || !/^[A-Za-z0-9]{6,32}$/.test(p)) return 'bad player id';
    if (seen.has(p)) return 'duplicate player';
    seen.add(p);
  }
  if (r.winner !== 'Town' && r.winner !== 'Mafia') return 'bad winner';
  if (!ROLES.includes(r.myRole)) return 'bad role';
  if (typeof r.survived !== 'boolean') return 'bad survived';
  if (!Number.isInteger(r.rounds) || r.rounds < cfg.minRounds || r.rounds > 200) return 'rounds out of range';
  if (!Number.isInteger(r.durationSec) || r.durationSec < cfg.minDurationSec || r.durationSec > 6 * 3600) return 'duration out of range';
  return null;
}

/** What one player gets. */
function rewardFor(report, cfg) {
  const won = teamOf(report.myRole) === report.winner;
  const c = cfg.rewards;
  const xp = c.baseXp + (won ? c.winXp : 0) + (report.survived ? c.survivedXp : 0);
  const coins = c.baseCoins + (won ? c.winCoins : 0);
  const rankPoints = won ? c.rankWin : c.rankLose;
  return { won, xp, coins, rankPoints };
}

/** Applies a reward to a progress object (returns the same object). */
function applyReward(progress, report, reward, isoNow) {
  addXp(progress, reward.xp);
  progress.rankPoints = Math.max(0, progress.rankPoints + reward.rankPoints);
  progress.games += 1;
  if (reward.won) progress.wins += 1; else progress.losses += 1;
  if (report.myRole === 'Mafia') { progress.mafiaGames += 1; if (reward.won) progress.mafiaWins += 1; }
  else { progress.citizenGames += 1; if (reward.won) progress.citizenWins += 1; }
  progress.lastReward = isoNow;
  return progress;
}

/** Number of matching reports needed before anything is paid: more than half of the players. */
function quorumFor(playerCount, cfg) {
  return Math.min(playerCount, Math.floor(playerCount * cfg.quorumRatio) + 1);
}

module.exports = { ROLES, xpForLevel, addXp, newProgress, teamOf, validateReport, rewardFor, applyReward, quorumFor };
