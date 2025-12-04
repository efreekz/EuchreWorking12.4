// Supabase Edge Function: /functions/v1/credit-game-reward
// Credits winners or logs losers at game end

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

const corsHeaders = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type"
};

serve(async (req) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders });
  }

  try {
    const supabaseClient = createClient(
      Deno.env.get("SUPABASE_URL") ?? "",
      Deno.env.get("SUPABASE_ANON_KEY") ?? "",
      {
        global: {
          headers: { Authorization: req.headers.get("Authorization")! }
        }
      }
    );

    // Get authenticated user
    const { data: { user }, error: authError } = await supabaseClient.auth.getUser();
    if (authError || !user) {
      return new Response(
        JSON.stringify({ error: "Unauthorized. Please log in." }),
        { status: 401, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    // Parse request body
    const body = await req.json().catch(() => ({}));
    const { lobby_id, lobby_fee, reward_amount, won_game } = body ?? {};

    if (!lobby_id || typeof lobby_fee !== "number" || typeof won_game !== "boolean") {
      return new Response(
        JSON.stringify({ error: "Invalid request. Provide lobby_id, lobby_fee, and won_game." }),
        { status: 400, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    // Get user's current balance
    const { data: userRow, error: fetchError } = await supabaseClient
      .from("users")
      .select("balance, username")
      .eq("id", user.id)
      .single();

    if (fetchError || !userRow) {
      return new Response(
        JSON.stringify({ error: "Failed to fetch user balance." }),
        { status: 500, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    const currentBalance = userRow.balance ?? 0;
    let newBalance = currentBalance;
    let transactionAmount = 0;
    let transactionType = "";
    let reason = "";

    if (won_game) {
      // Winner: Credit reward
      if (typeof reward_amount !== "number" || reward_amount <= 0) {
        return new Response(
          JSON.stringify({ error: "Invalid reward_amount for winner." }),
          { status: 400, headers: { ...corsHeaders, "Content-Type": "application/json" } }
        );
      }

      newBalance = currentBalance + reward_amount;
      transactionAmount = reward_amount; // Positive for credit
      transactionType = "game_won";
      reason = `Won ${lobby_fee} FZ game - Reward: ${reward_amount} FZ`;

      // Update balance
      const { error: updateError } = await supabaseClient
        .from("users")
        .update({ balance: newBalance })
        .eq("id", user.id);

      if (updateError) {
        console.error("Failed to update balance:", updateError);
        return new Response(
          JSON.stringify({ error: "Failed to credit game reward." }),
          { status: 500, headers: { ...corsHeaders, "Content-Type": "application/json" } }
        );
      }

    } else {
      // Loser: No balance change, just log
      transactionAmount = 0;
      transactionType = "game_lost";
      reason = `Lost ${lobby_fee} FZ game`;
    }

    // Log transaction
    const { data: transaction, error: txError } = await supabaseClient
      .from("transactions")
      .insert({
        user_id: user.id,
        amount: transactionAmount,
        transaction_type: transactionType,
        reason: reason,
        game_type: "fz",
        lobby_id: lobby_id,
        lobby_fee: lobby_fee
      })
      .select()
      .single();

    if (txError) {
      console.error("Failed to log transaction:", txError);
      // If winner, balance already credited - return success
      // If loser, transaction logging failed but no balance change needed
    }

    return new Response(
      JSON.stringify({
        success: true,
        balance: newBalance,
        transaction: transaction ?? null,
        message: won_game 
          ? `Congratulations! You won ${reward_amount} FZ.`
          : `Game result logged.`
      }),
      { status: 200, headers: { ...corsHeaders, "Content-Type": "application/json" } }
    );

  } catch (error) {
    console.error("credit-game-reward error:", error);
    return new Response(
      JSON.stringify({ error: "Internal server error." }),
      { status: 500, headers: { ...corsHeaders, "Content-Type": "application/json" } }
    );
  }
});
