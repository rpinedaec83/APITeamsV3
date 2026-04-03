Select *  FROM Seccion SE WITH (NOLOCK)          
 INNER JOIN Promocion PR WITH (NOLOCK) ON SE.IdPromocion = PR.IdPromocion                  
 INNER JOIN Periodo PE WITH (NOLOCK) ON PR.IdPeriodo = PE.IdPeriodo 

 where SE.Codigo in (
 '1C006-GEN.26.00531' 
 )

 update Periodo set EsTeams = 1 where IdPeriodo = 5311;

 SELECT *     
FROM Parametro WITH(NOLOCK)          
WHERE Nombre = 'EsTeams'  ;

update Parametro set Valor = 90 where Nombre = 'EsTeams';