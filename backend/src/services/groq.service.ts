import { env } from '../config/env.js';
import { HttpError } from '../lib/http-error.js';

export interface GroqMessage {
  role: 'system' | 'user' | 'assistant';
  content: string;
}

const GROQ_URL = 'https://api.groq.com/openai/v1/chat/completions';
const REQUEST_TIMEOUT_MS = 30_000;

/**
 * Thin wrapper around the Groq chat completions API.
 *
 * The key lives here and only here. The dashboard calls this server, and this server
 * calls Groq - so the browser never receives a credential, which is why the assistant
 * is not implemented as a direct fetch from React.
 */
export const groqService = {
  get isConfigured(): boolean {
    return env.hasGroq;
  },

  async complete(messages: GroqMessage[], options: { temperature?: number } = {}): Promise<string> {
    if (!env.hasGroq) {
      throw HttpError.serviceUnavailable(
        'The assistant is not configured. Set GROQ_API_KEY in backend/.env and restart the server.',
      );
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), REQUEST_TIMEOUT_MS);

    try {
      const response = await fetch(GROQ_URL, {
        method: 'POST',
        signal: controller.signal,
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${env.GROQ_API_KEY}`,
        },
        body: JSON.stringify({
          model: env.GROQ_MODEL,
          messages,
          temperature: options.temperature ?? 0.6,
        }),
      });

      if (!response.ok) {
        // Groq's own error text can quote the request, so it is logged rather than
        // returned to the browser.
        console.error('[groq] request failed', response.status, await response.text());

        throw HttpError.serviceUnavailable('The assistant is temporarily unavailable. Please try again.');
      }

      const payload = (await response.json()) as {
        choices?: { message?: { content?: string } }[];
      };

      const content = payload.choices?.[0]?.message?.content?.trim();

      if (!content) {
        throw HttpError.serviceUnavailable('The assistant returned an empty response. Please try again.');
      }

      return content;
    } catch (error) {
      if (error instanceof HttpError) {
        throw error;
      }

      if (error instanceof Error && error.name === 'AbortError') {
        throw HttpError.serviceUnavailable('The assistant took too long to respond. Please try again.');
      }

      console.error('[groq] unexpected failure', error);

      throw HttpError.serviceUnavailable('The assistant is temporarily unavailable. Please try again.');
    } finally {
      clearTimeout(timeout);
    }
  },
};
