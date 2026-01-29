using Microsoft.AspNetCore.Mvc;

namespace MooreHotels.WebAPI.Controllers;

[ApiController]
[Route("api/payment-simulator")]
public class PaymentSimulatorController : ControllerBase
{
    [HttpGet]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult SimulatePaystack(
        [FromQuery] string code, 
        [FromQuery] decimal amount, 
        [FromQuery] string email,
        [FromQuery] string? redirectUrl = null)
    {
        // Use double curly braces {{ }} to escape them in C# interpolated strings
        // This ensures the JavaScript template literals `${baseOrigin}` work correctly.
        var html = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <title>Moore Hotels | Paystack Simulator</title>
            <script src='https://cdn.tailwindcss.com'></script>
            <style>
                @import url('https://fonts.googleapis.com/css2?family=Inter:wght@400;600;700&display=swap');
                body {{ font-family: 'Inter', sans-serif; background-color: #f3f4f6; }}
                .loader {{ border-top-color: #2563eb; animation: spinner 1s linear infinite; }}
                @keyframes spinner {{ 0% {{ transform: rotate(0deg); }} 100% {{ transform: rotate(360deg); }} }}
            </style>
        </head>
        <body class='flex items-center justify-center min-h-screen p-4'>
            <div class='bg-white p-8 rounded-2xl shadow-2xl w-full max-w-md border border-gray-100'>
                <div class='flex items-center justify-between mb-8'>
                    <div class='bg-blue-600 text-white font-bold p-3 rounded-xl shadow-lg shadow-blue-600/20'>M</div>
                    <div class='text-right'>
                        <p class='text-[10px] text-gray-400 font-bold uppercase tracking-widest'>Simulation Mode</p>
                        <p class='text-blue-500 font-black italic text-lg'>paystack</p>
                    </div>
                </div>

                <div class='mb-6 space-y-1'>
                    <h2 class='text-gray-400 text-xs font-bold uppercase tracking-wider'>Booking Reference</h2>
                    <p class='text-2xl font-black text-gray-900'>{code}</p>
                </div>

                <div class='bg-blue-50 p-6 rounded-2xl mb-8 border border-blue-100 shadow-inner'>
                    <div class='flex justify-between items-center'>
                        <span class='text-blue-600 font-semibold'>Total Payable</span>
                        <span class='text-2xl font-black text-blue-900'>NGN {amount:N2}</span>
                    </div>
                    <div class='mt-2 pt-2 border-t border-blue-200/50'>
                        <p class='text-[10px] text-blue-400 uppercase font-bold tracking-tighter'>Account</p>
                        <p class='text-sm text-blue-700 font-medium'>{email}</p>
                    </div>
                </div>

                <div id='statusContainer' class='hidden mb-6 p-4 rounded-xl text-sm font-medium'></div>

                <button onclick='confirmPayment()' id='payBtn' class='w-full bg-blue-600 hover:bg-blue-700 text-white font-bold py-5 rounded-2xl transition-all shadow-xl shadow-blue-600/30 active:scale-[0.98] flex items-center justify-center gap-3'>
                    <span id='btnText'>Pay NGN {amount:N2}</span>
                    <div id='btnLoader' class='hidden w-5 h-5 border-2 border-white/30 rounded-full loader'></div>
                </button>

                <div class='mt-8 pt-6 border-t border-gray-100 text-center'>
                    <p class='text-[10px] text-gray-400 mb-2 font-bold uppercase tracking-widest'>Technical Context</p>
                    <code class='text-[10px] bg-gray-50 text-gray-500 px-3 py-1 rounded-full border border-gray-200'>
                        Return Path: {redirectUrl ?? "Same Port"}
                    </code>
                </div>
            </div>

            <script>
                async function confirmPayment() {{
                    const btn = document.getElementById('payBtn');
                    const btnText = document.getElementById('btnText');
                    const btnLoader = document.getElementById('btnLoader');
                    const statusContainer = document.getElementById('statusContainer');

                    btn.disabled = true;
                    btnText.innerText = 'Verifying Transaction...';
                    btnLoader.classList.remove('hidden');
                    statusContainer.classList.add('hidden');

                    try {{
                        const targetUrl = window.location.origin + '/api/bookings/{code}/verify-paystack';
                        
                        const response = await fetch(targetUrl, {{
                            method: 'POST',
                            headers: {{ 'Accept': 'application/json' }}
                        }});
                        
                        const result = await response.json();

                        if (response.ok) {{
                            statusContainer.innerText = '✓ ' + result.message;
                            statusContainer.className = 'mb-6 p-4 rounded-xl text-sm font-medium bg-emerald-50 text-emerald-700 border border-emerald-100 block';
                            btn.className = 'w-full bg-emerald-500 text-white font-bold py-5 rounded-2xl transition-all shadow-xl shadow-emerald-500/30';
                            btnText.innerText = 'Success! Redirecting...';
                            btnLoader.classList.add('hidden');
                            
                            setTimeout(() => {{
                                // Detection logic to return to the specific local port
                                let baseOrigin = '{redirectUrl}' || '';
                                if (!baseOrigin && document.referrer) {{
                                    try {{ baseOrigin = new URL(document.referrer).origin; }} catch(e) {{}}
                                }}
                                if (!baseOrigin) baseOrigin = window.location.origin;

                                // Destination: /booking-confirmation/{{CODE}}
                                const bookingCode = result.data.bookingCode || '{code}';
                                window.location.href = baseOrigin + '/booking-confirmation/' + bookingCode;
                            }}, 1500);
                        }} else {{
                            throw new Error(result.message || 'Verification failed');
                        }}
                    }} catch (e) {{
                        statusContainer.innerText = '✕ ' + e.message;
                        statusContainer.className = 'mb-6 p-4 rounded-xl text-sm font-medium bg-rose-50 text-rose-700 border border-rose-100 block';
                        btn.disabled = false;
                        btnText.innerText = 'Retry Payment';
                        btnLoader.classList.add('hidden');
                    }}
                }}
            </script>
        </body>
        </html>";

        return Content(html, "text/html");
    }
}