import type { Messages } from '../Messages'

// Language endonyms stay in their own script in both catalogues: a Bangla clerk choosing
// "বাংলা" must not have the option rename itself after the choice takes effect.
export const bn: Messages = {
  app: {
    name: 'গার্মেন্টস ইআরপি',
    sectionAdmin: 'প্রশাসন',
  },
  locale: {
    switch: 'ভাষা',
    en: 'English',
    bn: 'বাংলা',
  },
  common: {
    loading: 'লোড হচ্ছে…',
    retry: 'আবার চেষ্টা করুন',
    emptyTitle: 'এখনো কোনো তথ্য নেই',
    emptyBody:
      'যে মডিউল এই তথ্য লেখে তা তৈরি হলে এখানে দেখা যাবে। এই স্ক্রনে কোনো কৃত্রিম তথ্য রাখা হয়নি।',
    signedInAs: '{email} হিসেবে প্রবেশ করা হয়েছে',
  },
  login: {
    title: 'সাইন ইন',
    subtitle: 'প্রশাসনকর্তা যে তথ্য দিয়েছেন সেটি ব্যবহার করুন।',
    tenant: 'কোম্পানি',
    email: 'ইমেইল',
    password: 'পাসওয়ার্ড',
    submit: 'সাইন ইন করুন',
    submitting: 'প্রবেশ করা হচ্ছে…',
    credentialsRequired: 'কোম্পানি, ইমেইল ও পাসওয়ার্ড তিনটিই দরকার।',
  },
  admin: {
    workspace: 'কর্মক্ষেত্র',
    dashboard: 'ড্যাশবোর্ড',
    openedOn: 'খোলা হয়েছে {date}',
  },
  dashboard: {
    title: 'ড্যাশবোর্ড',
    ledgerPending:
      'হিসাব, উৎপাদন ও ইনভেন্টরি স্ক্রিন P2 থেকে P7 পর্যন্ত ধাপে ধাপে আসবে। ধাপটি সম্পূর্ণ না হওয়া পর্যন্ত সংখ্যাগুলো অনুপস্থিত থাকবে, কল্পনা করা হবে না।',
  },
  errors: {
    missingBaseUrl: 'এই ডিপ্লয়মেন্টে API ঠিকানা নির্ধারণ করা হয়নি।',
    network: 'সার্ভারে পৌঁছানো যায়নি।',
    unauthorized: 'প্রদত্ত তথ্য গ্রহণ করা হয়নি।',
    rejected: 'সার্ভার অনুরোধ প্রত্যাখ্যান করেছে (স্ট্যাটাস {status})।',
    unexpected: 'অনুরোধটি সম্পন্ন করা যায়নি।',
  },
}
