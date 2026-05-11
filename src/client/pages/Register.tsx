import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useRegister } from '../services/auth';
import { useToast } from '../components/toaster';
import { Button, Card, Field, Input } from '../components/ui';

export default function Register() {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const register = useRegister();
  const toast = useToast();

  // The mismatch check is purely client-side guard rails; the server
  // doesn't see `confirm`. The button stays disabled while the two
  // fields disagree so the network round-trip never fires.
  const mismatched = confirm.length > 0 && password !== confirm;

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-semibold text-white mb-6">Create an account</h1>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (mismatched) return;
            register.mutate(
              { userName, password },
              {
                onSuccess: () => toast.success(`Welcome, ${userName}.`),
              },
            );
          }}
        >
          <Field label="Username">
            <Input value={userName} onChange={(e) => setUserName(e.target.value)} autoFocus required />
          </Field>
          <Field label="Password">
            <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)} required minLength={8} />
          </Field>
          <Field label="Confirm password">
            <Input
              type="password"
              value={confirm}
              onChange={(e) => setConfirm(e.target.value)}
              required
              minLength={8}
              aria-invalid={mismatched || undefined}
            />
          </Field>
          {mismatched && <p className="text-sm text-rose-400">Passwords don't match.</p>}
          {register.error && (
            <p className="text-sm text-rose-400">{(register.error as Error).message ?? 'Registration failed.'}</p>
          )}
          <Button type="submit" disabled={register.isPending || mismatched} className="w-full">
            {register.isPending ? 'Creating account…' : 'Create account'}
          </Button>
        </form>
        <p className="mt-4 text-sm text-slate-400 text-center">
          Already have an account?{' '}
          <Link to="/login" className="text-indigo-300 hover:text-indigo-200 underline">
            Sign in
          </Link>
        </p>
      </Card>
    </div>
  );
}
