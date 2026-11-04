// Pages that are still server-rendered Razor pages. They open as normal full page loads.
// When a page moves into the Vue app, point its entry here at the router path and update the
// matching link in Views/Shared/_Layout.cshtml.
export const LEGACY = {
  dashboard: '/Board',
  matches: '/Matches',
  unavailability: '/Unavailability',
  users: '/Users',
  allocation: '/Allocation',
  account: '/Identity/Account/Manage',
  refereeHome: '/Referee',
  refereeMatches: '/Referee/MyMatches',
  refereeAvailability: '/Referee/MyAvailability',
};
