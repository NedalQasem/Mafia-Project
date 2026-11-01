'use strict';
// The built-in word filter list of the chat: well-known Arabic (and Franco-Arabic / English) insults and obscene words.
// The spelling is compared after src/chat.js normalizes it (diacritics, alef/yaa/taa-marbuta forms), so only one spelling per word is needed.
//  - EXACT: matched as a whole word, because they are short and would hit innocent words as a part ("كس" inside "كسر").
//  - CONTAINS: matched anywhere inside a word, because they are long or distinctive enough ("شرموط" also catches "شرموطة" and "ابن الشرموطة").
// Extra words can be added from config.json (chat.bannedWords = contains, chat.bannedExact = whole words); set
// chat.useDefaultBannedWords to false to switch this list off.

const EXACT = [
  // arabic
  'كس', 'كسك', 'كسم', 'كسمك', 'كسها', 'طيز', 'طيزك', 'زب', 'زبي', 'زبك', 'نيك', 'نيج', 'ناك', 'عرص', 'خول', 'خرا', 'خره', 'بزاز', 'سكس',
  // franco-arabic and english
  'ayre', 'ayri', 'zeb', 'zebi', 'neek', 'koss', 'kos',
  'dick', 'cock', 'ass', 'sex', 'pussy', 'cum', 'tits', 'prick', 'fag', 'retard', 'anal', 'penis', 'vagina', 'dildo', 'xxx', 'pedo', 'paedo', 'kys', 'stfu',
  // spelling tricks of the same words
  'fuk', 'fck', 'fuc', 'fcuk', 'a55', 'a$', 'btch', 'b1tch', 'sh1t', 'cnt',
];

const CONTAINS = [
  // arabic
  'شرموط', 'شراميط', 'قحب', 'منيوك', 'منيك', 'متناك', 'كسمك', 'كسامك', 'كساختك', 'كسختك', 'ديوث', 'لوطي', 'سكسي', 'نيكني', 'انيكك', 'اينيك',
  // franco-arabic and english
  'sharmoot', 'sharmouta', 'sharmota', 'kosomak', 'kosomk', 'kossomak', 'manyak', 'manyok', 'fuck', 'bitch', 'shit', 'asshole', 'cunt', 'whore', 'slut', 'nigger', 'nigga', 'porn', 'bastard',
  'dickhead', 'dumbass', 'jackass', 'douche', 'wanker', 'bollocks', 'faggot', 'twat', 'blowjob', 'handjob', 'cumshot', 'jerkoff', 'orgasm', 'hentai', 'pedophile', 'paedophile', 'motherfuck', 'fvck', 'phuck', 'b1tch', 'sh1t',
];

module.exports = { EXACT, CONTAINS };
