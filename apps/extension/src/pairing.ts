export interface PairingCompletion {
  current(): Promise<boolean>;
  connect(token: string): Promise<void>;
  revoke(token: string): Promise<void>;
}
// A credential delivered during cancellation must never remain usable server-side.
export async function completePairing(
  token: string,
  actions: PairingCompletion,
) {
  if (!(await actions.current())) {
    await actions.revoke(token);
    return false;
  }
  try {
    await actions.connect(token);
  } catch (error) {
    await actions.revoke(token);
    throw error;
  }
  if (!(await actions.current())) {
    await actions.revoke(token);
    return false;
  }
  return true;
}
