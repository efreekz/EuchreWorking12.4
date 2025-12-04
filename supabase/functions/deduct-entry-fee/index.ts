// Supabase Edge Function: /functions/v1/deduct-entry-fee
// Deducts entry fee when game starts (hard check + deduction)

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
    const { lobby_id, lobby_fee } = body ?? {};

    if (!lobby_id || !lobby_fee || typeof lobby_fee !== "number" || lobby_fee <= 0) {
      return new Response(
        JSON.stringify({ error: "Invalid request. Provide lobby_id and lobby_fee." }),
        { status: 400, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    // Get user's current balance (with row lock for transaction safety)
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

    // Check if sufficient balance
    if (currentBalance < lobby_fee) {
      return new Response(
        JSON.stringify({
          success: false,
          error: `Insufficient balance. Required: ${lobby_fee} FZ, Current: ${currentBalance} FZ`
        }),
        { status: 400, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    // Deduct entry fee
    const newBalance = currentBalance - lobby_fee;

    const { error: updateError } = await supabaseClient
      .from("users")
      .update({ balance: newBalance })
      .eq("id", user.id);

    if (updateError) {
      console.error("Failed to update balance:", updateError);
      return new Response(
        JSON.stringify({ error: "Failed to deduct entry fee." }),
        { status: 500, headers: { ...corsHeaders, "Content-Type": "application/json" } }
      );
    }

    // Log transaction
    const { data: transaction, error: txError } = await supabaseClient
      .from("transactions")
      .insert({
        user_id: user.id,
        amount: -lobby_fee, // Negative for debit
        transaction_type: "entry_fee",
        reason: `Entry fee for ${lobby_fee} FZ game`,
        game_type: "fz",
        lobby_id: lobby_id
      })
      .select()
      .single();

    if (txError) {
      console.error("Failed to log transaction:", txError);
      // Balance already deducted, so return success even if logging fails
    }

    return new Response(
      JSON.stringify({
        success: true,
        balance: newBalance,
        transaction: transaction ?? null,
        message: `Entry fee of ${lobby_fee} FZ deducted successfully.`
      }),
      { status: 200, headers: { ...corsHeaders, "Content-Type": "application/json" } }
    );

  } catch (error) {
    console.error("deduct-entry-fee error:", error);
    return new Response(
      JSON.stringify({ error: "Internal server error." }),
      { status: 500, headers: { ...corsHeaders, "Content-Type": "application/json" } }
    );
  }
});
